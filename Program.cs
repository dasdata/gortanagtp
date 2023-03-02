using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio; 
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Globalization;
using System.Web;
using System.Reflection;
using Newtonsoft.Json;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

namespace SpeechToText
{
    class Program
    {
        // AZURE 
        static string YourServiceRegion = "eastus";
        static string YourSubscriptionKey = "xxxxx-xxxxxxxxxxx-xxxxxxxxxxx-xxxxxxxxxx-xxxxxxxx"; // change to your azure cognitive key 
        private static string _cultureLng = "en-US"; // https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.getcultures?view=net-7.0
        private static string _synthVoice = "en-US-JennyNeural"; // https://github.com/microsoft/cognitive-services-speech-sdk-js/blob/master/src/sdk/SpeechSynthesizer.ts

        // OPEN AI
        private static string _txtContext = "You will be kind and nice to humans offering out of the box solutions to any problem.";
        static string _oiKey = "xxxxx-xxxxxxxxxxx-xxxxxxxxxxx-xxxxxxxxxx-xxxxxxxx"; // change to your key

        static string _oAIChatCompletion = "https://api.openai.com/v1/chat/completions";
        static string _oAICompletion = "https://api.openai.com/v1/completions";
        static string _oAIModeration = "https://api.openai.com/v1/moderations";
        static string _oAIImages = "https://api.openai.com/v1/images/generations";
        static string _engineAI = "gpt-3.5-turbo";
        static string _shortyMemory = "";
        // Get the computer name
        static string computerName = Environment.MachineName;
        static string username = Environment.UserName;
        static string _avatarBot, _avatarHuman =  "";
        static int _timeOut = 5;
        private static readonly HttpClient client = new HttpClient();
        static string _env, _folderPath = "";

        public class ChatCompletionRequest
        {
            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("messages")]
            public List<Message> Messages { get; set; }
        }

        public class Message
        {
            [JsonProperty("role")]
            public string Role { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }

            public Message(string role, string content)
            {
                Role = role;
                Content = content;
            }
        }

        public class ChatCompletionResponse
        {
            [JsonProperty("choices")]
            public List<Choice> Choices { get; set; }
        }

        public class Choice
        {
            [JsonProperty("message")]
            public Message Message { get; set; }
        }

        static async Task Main(string[] args)
        {
            cmdWelcomeBot();

            if (_env == "1")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{_avatarBot}");

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                string _welcomeGreeting = "Hello " + username + ", How may I help you today? Say RESET when our conversation is done!";
                
                Task writingTask = Task.Run(() => TypeTextWithCursor(_welcomeGreeting, 2));
                Task speechTask = Task.Run(() => SynthesisToSpeakerAsync(_welcomeGreeting));

                // Wait for both tasks to complete
                Task.WaitAll(writingTask, speechTask);


                await ConversationStart();
            }
            else
            {
                Console.WriteLine("Start conversation...");
                string val = Console.ReadLine();
                await cmdDialog(val);
            }
        }


        static async Task ConversationStart()
        {
            string _generatedContent = "";
            CultureInfo ci = new CultureInfo(_cultureLng);
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // speech 2 text  azure way
            var speechConfig = SpeechConfig.FromSubscription(YourSubscriptionKey, YourServiceRegion);
            // language and voice settings
            speechConfig.SpeechSynthesisVoiceName = _synthVoice;
            speechConfig.SpeechRecognitionLanguage = _cultureLng;

            string myRequest = await FromMic(speechConfig);
            if (myRequest != "")
            {
                string input = myRequest.ToLower();

                if (input.Contains("reset")) // clears the short memory context 
                {
                    Console.WriteLine("Performing reset action...");
                    _shortyMemory = "";
                    cmdWelcomeBot();

                }
                else
                {
                    // ChatGTP - API call 
                    var _chatTaskResponse = CallApiWithChatAsync(myRequest);
                    _generatedContent = await _chatTaskResponse;
                
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"{_avatarBot}");

                    Console.ForegroundColor = ConsoleColor.DarkGreen; 
                    //  Console.WriteLine($" {text}");

                    // make this sync between text and voice
                    Task writingTask = Task.Run(() => TypeTextWithCursor(_generatedContent,2));
                    Task speechTask = Task.Run(() =>  SynthesisToSpeakerAsync(_generatedContent) );

                    // Wait for both tasks to complete
                    Task.WaitAll(writingTask, speechTask);

                    // OPEN AI - GTP call 
                    // var task = cmdModerateAsync(myRequest); 
                    //var task = cmdGenerateContentAsync(myRequest);
                    //_generatedContent = await task;

                    // speak back to user with voice using azure text 2 speech
                    // await SynthesisToSpeakerAsync(_generatedContent); 
                }


            } 
            // loop back 
            await ConversationStart();
        }


        // this will get your text input
        private static async Task cmdDialog(string myRequest)
        { 
            //   var task = cmdGenerateContentAsync(myRequest); 
            var task = CallApiWithChatAsync(myRequest);
            string _generatedContent = await task;

            await SynthesisToSpeakerAsync(_generatedContent);
            string val = Console.ReadLine();
      
            await cmdDialog(val);
        }
         
        // call to API chat 
        static async Task<string> CallApiWithChatAsync(string _keyTopic)
        { 
            string _sysprompt = "My name is "+ username + ", earlier we spoke about: " + _shortyMemory + " but if empty or topic if is not related to " + _txtContext + " just skip and focus on user topic. "; // + _keyTopic + ". ";  // you can define context here
            _shortyMemory += _keyTopic + ", ";

            var httpClient = new HttpClient();
            var requestData = new ChatCompletionRequest
            {
                Model = _engineAI,
                Messages = new List<Message>
            {
               new Message("assistant", _sysprompt),
               new Message("user", _keyTopic)
            }
            };
            var requestContent = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _oiKey);
            var response = await httpClient.PostAsync(_oAIChatCompletion, requestContent);
            var responseJson = await response.Content.ReadAsStringAsync();
            var chatCompletion = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseJson);

            // return (chatCompletion.Choices[0].Message.Content);
            string _choicesResp = "";
            if (chatCompletion != null)
            {
                foreach (var choice in chatCompletion.Choices)
                {
                    _choicesResp += (choice.Message.Content);
                }
            }
            return _choicesResp;
        }


        // this will convert the text into audio
        public static async Task SynthesisToSpeakerAsync(string text)
        { 

            SpeechConfig config = SpeechConfig.FromSubscription(YourSubscriptionKey, YourServiceRegion);
            config.SpeechSynthesisLanguage = _cultureLng;
            config.SpeechSynthesisVoiceName = _synthVoice;
            using (SpeechSynthesizer synthesizer = new SpeechSynthesizer(config))
            {
                using (SpeechSynthesisResult result = await synthesizer.SpeakTextAsync(text))
                {
                    if (result.Reason != ResultReason.SynthesizingAudioCompleted) ;
                }
            } 
        }

        // this will save the audio file (optional)
        static async Task SynthesizeFileAudioAsync(string _fileName, string _text)
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(_cultureLng);
            var config = SpeechConfig.FromSubscription(YourSubscriptionKey, YourServiceRegion);
            config.SpeechSynthesisVoiceName = _synthVoice;
            config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio48Khz192KBitRateMonoMp3);
            //  https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/rest-text-to-speech?tabs=streaming#audio-outputs
            var audioConfig = AudioConfig.FromWavFileOutput(_folderPath + "/" + _fileName + ".mp3");
            var synthesizer = new SpeechSynthesizer(config, audioConfig);
            await synthesizer.SpeakTextAsync(_text);
        }

        // this will convert your voice in text 
        async static Task<string> FromMic(SpeechConfig speechConfig)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{_avatarHuman} ");
            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
          
            var result = await recognizer.RecognizeOnceAsync();
            string strResult = result.Text;

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            // making a default timeout if silent
            if (strResult == "")   {   _timeOut -= 1; } 
            else  {
                TypeTextWithCursor(strResult, 2);
                // Console.WriteLine($"{strResult}");   
            }

            // go to main 
            if (_timeOut < 1) {  _timeOut = 5;  cmdWelcomeBot();  } 

            return strResult;
        }

        public static string cleanStr(string input)
        {
            Regex r = new Regex("(?:[^a-z0-9 ]|(?<=['\"])s)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            return r.Replace(input, String.Empty);
        }

        // completion 
        static async Task<string> cmdGenerateContentAsync(string _keyTopic)
        {
            string _resultGen = "";
            string _prompt = "Earlier we spoke about: " + _shortyMemory + " but if empty or topic if is not related to " + _txtContext + " just skip and focus on " + _keyTopic + ". ";  // you can define context here
            _shortyMemory += _keyTopic + ", ";
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_oAICompletion),
                Headers = { { "Authorization", "Bearer " + _oiKey }, },
                Content = new StringContent("{\n  \"model\": \"" + _engineAI + "\",\n\t\"prompt\": \"" + _prompt + "\",\n\t\"max_tokens\": 120,\n\t\"temperature\": 1,\n\t\"frequency_penalty\": 0,\n\t\"presence_penalty\": 0,\n\t\"top_p\": 1,\n\t\"stop\": null\n}")
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();

                JToken o = JObject.Parse(body);
                // string myResult = deserialized.SelectToken("choices").ToString();
                JArray _content = (JArray)o["choices"];
                _resultGen = (string)_content[0]["text"].ToString();
                // _resultGen = (myResult);
            }

            return _resultGen;

        }


        // image generation
        static async Task<string> cmdGenerateImageAsync(string _prompt, int _NoImg)
        {
            string _resultGen = "";
            string _fileLocalName = "";
            string _fileRandomName = System.IO.Path.GetRandomFileName(); // this will generate random file name
            string _tempDire = System.IO.Path.GetTempPath(); /*Server.MapPath("")*/; // System.IO.Path.GetTempPath();   
            _fileLocalName = _tempDire + _fileRandomName + ".png";
            string _fileLocalJPGName = "";
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_oAIImages),
                Headers = { { "Authorization", "Bearer " + _oiKey }, },     //  256x256    512x512   1024x1024
                Content = new StringContent("{\n    \"prompt\": \"" + _prompt + "\",\n    \"n\": " + _NoImg + ",\n    \"size\": \"512x512\" \n}")
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                JToken o = JObject.Parse(body);
                // _resultGen = o.SelectToken("data").ToString();

                JArray _content = (JArray)o["data"];
                _resultGen = (string)_content[0]["url"].ToString();

                string _picUrl = _resultGen;

                using (WebClient downClient = new WebClient())
                {
                    // downClient.DownloadFile(new Uri(_picUrl), _fileLocalName);
                    // OR 
                    downClient.DownloadFileAsync(new Uri(_picUrl), _fileLocalName);
                }

                //Image png = Image.FromFile(_fileLocalName);
                //_fileLocalJPGName = _tempDire + @"/" + _fileRandomName + ".jpg";
                //png.Save(_tempDire, ImageFormat.Jpeg);
                //png.Dispose(); 
                //   File.Delete(_fileLocalName);
            }
            return _fileLocalName;
        }

        static async Task<bool> cmdModerateAsync(string _input)
        {
            bool _statusResponse = false;

            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_oAIModeration),
                Headers =
                     {
                       { "Authorization", "Bearer "+ _oiKey },
                     },
                Content = new StringContent("{\"input\": \"" + _input + "\"}")
                { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();

                JToken deserialized = JObject.Parse(body);
                string myResult = deserialized.SelectToken("results").ToString();

                if (myResult.Contains("true")) { _statusResponse = true; }
            }

            return _statusResponse;

        }




        static void cmdWelcomeBot()
        {
            // let's start the bot
            Console.Clear(); 
            Console.OutputEncoding = Encoding.UTF8;

            Console.ForegroundColor = ConsoleColor.Yellow;
            _avatarHuman = GetRandomHumanFace();
            Console.Write(_avatarHuman);
            Console.Write("\t");
            Console.ForegroundColor = ConsoleColor.Green;
            _avatarBot = GetRandomAsciiArt();
            Console.WriteLine(_avatarBot);
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine("Choose from following options:");
            Console.WriteLine(" 1 for VOICE or 2 for TEXT INPUTS");
            Console.WriteLine(" Say 'reset' to start over!");
            //    Console.WriteLine(" P for PAUSE or R for RESTART");  !! TO DO
            //    Console.WriteLine(" Q for QUIT"); 
            _env = Console.ReadLine(); 

        }

        static string GetRandomAsciiArt()
        {
            var asciiArts = new List<string>
        {
            @"(ง'̀-'́)ง",
            @"ಠ_ಠ",
            @"ノ( º _ ºノ)",
            @"(✿◠‿◠)",
           };
            var random = new Random();
            var index = random.Next(asciiArts.Count);
            return asciiArts[index];

        }


        public static string GetRandomHumanFace()
        {
            var faces = new List<string>()
          {
        "( ͡° ͜ʖ ͡°)",
        "(づ｡◕‿‿◕｡)づ",
           };

            var random = new Random();
            var index = random.Next(faces.Count);
            return faces[index];
        }


        public static void TypeTextWithCursor(string text, int delay)
        {  // Scroll to the bottom of the console window
           // Console.SetCursorPosition(0, Console.WindowHeight - 1);
          
            int width = Console.WindowWidth;
            int mid = width / 2;
            int column = 0;

            foreach (char c in text)
            {
                Console.Write(c);
                Thread.Sleep(delay);

                // Wrap the text at the specified column
                column++;
                if (column >= mid && c == ' ')
                {
                    Console.WriteLine();
                    column = 0;
                }

                // Print the cursor symbol
                Console.Write(column >= mid ? '_' : ' ');
                Thread.Sleep(40);

                // Delete the cursor symbol
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Console.Write(' ');
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
            }

            // Print the final cursor symbol
            Console.WriteLine();
        }


    }
}
 



//try
//{
//    // you add some context from the training file 
//    _folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).ToString();
//    string _trainFile = _folderPath + "/Training.txt";
//    using (StreamReader streamReader = new StreamReader(_trainFile, Encoding.UTF8))
//        _txtContext = cleanStr(streamReader.ReadToEnd());
//    Console.WriteLine("Training loaded...");
//}
//catch
//{
//    Console.WriteLine("Training.txt file missing from desktop... loading dialog without any context.");
//}