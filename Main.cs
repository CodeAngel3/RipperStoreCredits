using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using Newtonsoft.Json;
using VRC.Core;
using Harmony;
using System.IO;
using System.Threading;
using System.Globalization;
using VRC;

namespace RipperStoreCreditsUploader
{
    public static class BuildInfo
    {
        public const string Name = "RipperStoreCredits";
        public const string Author = "CodeAngel";
        public const string Company = "https://ripper.store";
        public const string Version = "2";
        public const string DownloadLink = null;
    }

    public class Main : MelonMod
    {
        private static Config Config { get; set; }
        private static Queue<ApiAvatar> _queue = new Queue<ApiAvatar>();
        private static HttpClient _http = new HttpClient();
        private static HarmonyMethod GetPatch(string name) { return new HarmonyMethod(typeof(Main).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        public override void OnApplicationStart()
        {
            if (!File.Exists("RipperStoreCredits.txt"))
            {
                Console.WriteLine("\n------- !! -------\n\n\nGenerated new config, please insert your apiKey into 'RipperStoreCredits.txt'\n\n\n------- !! -------\n");
                File.WriteAllText("RipperStoreCredits.txt", JsonConvert.SerializeObject(new Config { apiKey = "place_apiKey_here", LogToConsole = true }, Formatting.Indented));
            }
            else { Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("RipperStoreCredits.txt")); }


            //Big thx to keafy for this patch ^^
            foreach (var methodInfo in typeof(AssetBundleDownloadManager).GetMethods().Where(p => p.GetParameters().Length == 1 && p.GetParameters().First().ParameterType == typeof(ApiAvatar) && p.ReturnType == typeof(void)))
            {
                Harmony.Patch(methodInfo, GetPatch("AvatarToQueue"));
            }
            new Thread(CreditWorker).Start();
        }
        private static void AvatarToQueue(ApiAvatar __0)
        {
            _queue.Enqueue(__0);
        }

        private static void CreditWorker()
        {
            for (; ; )
            {
                try
                {
                    if (_queue.Count != 0 && RoomManager.field_Internal_Static_ApiWorld_0 != null)
                    {
                        var __0 = _queue.Peek();
                        _queue.Dequeue();

                        var obj = new ExpandoObject() as IDictionary<String, object>;

                        obj["hash"] = BitConverter.ToString(Encoding.UTF8.GetBytes(__0.id + "|" + __0.assetUrl + "|" + __0.imageUrl + "|" + RoomManager.field_Internal_Static_ApiWorld_0.id));
                        foreach (PropertyInfo p in __0.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (p.GetValue(__0) == null) p.SetValue(__0, "null");
                            obj[p.Name.ToString()] = p.GetValue(__0).ToString();
                        }
                        StringContent data = new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");
                        var res = _http.PostAsync($"https://api.ripper.store/clientarea/credits/submit?apiKey={Config.apiKey}&v={BuildInfo.Version}", data).GetAwaiter().GetResult();

                        var name = __0.name.Length > 32 ? __0.name.Substring(0, 32) : __0.name;
                        if (Config.LogToConsole)
                        {
                            switch (res.StatusCode)
                            {
                                case (HttpStatusCode)201:
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"Successfully send {name} to API, verification pending..");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    break;
                                case (HttpStatusCode)409:
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"Failed to send {name}, already exists..");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    break;
                                case (HttpStatusCode)401:
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Invalid API Key Provided");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    break;
                                case (HttpStatusCode)403:
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Your Account got suspended.");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    break;
                                case (HttpStatusCode)426:
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("You are using an old Version of this Mod, please update via our Website (https://ripper.store/clientarea) > Credits Section");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    break;
                                case (HttpStatusCode)429:
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("You are sending too many Avatars at the same time, slow down..");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error while sending Avatar to API");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                Thread.Sleep(500);
            }
        }
    }

}
