using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using MelonLoader;
using HarmonyLib;
using System.IO;
using XDMessaging;
using System.Collections;
using System.Net.Http;
using System.Diagnostics;
using VRC.Core;
using System.ComponentModel;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonInfo(typeof(Ripper.Store.Internal.Main), "RipperStoreCredits", "10", "KeafyIsHere, CodeAngel & LargestBoi")]

namespace Ripper.Store.Internal
{
    public class Main : MelonLoader.MelonMod
    {
        private static HashSet<string> cache = new HashSet<string>();
        private static Queue<ApiAvatar> _queue = new Queue<ApiAvatar>();
        private static IXDBroadcaster _broadcaster;
        private static XDMessagingClient _messaging_client;
        private static HarmonyMethod GetPatch(string name) { return new HarmonyMethod(typeof(Main).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        public override void OnApplicationStart()
        {
			foreach (var process in Process.GetProcessesByName("RipperStoreExternal"))
            {
                process.Kill();
            }
            File.WriteAllBytes("RipperStoreExternal.exe", Properties.Resources.RipperStore_External);

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Path.Combine(Environment.CurrentDirectory, "RipperStoreExternal.exe"),
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            Process _process = Process.Start(startInfo);
            _process.OutputDataReceived += Proc_OutputDataReceived;
            _process.BeginOutputReadLine();

            _messaging_client = new XDMessagingClient();
            _broadcaster = _messaging_client.Broadcasters.GetBroadcasterForMode(XDTransportMode.Compatibility);

            foreach (var methodInfo in typeof(AssetBundleDownloadManager).GetMethods().Where(p => p.GetParameters().Length == 1 && p.GetParameters().First().ParameterType == typeof(ApiAvatar) && p.ReturnType == typeof(void)))
            {
                HarmonyInstance.Patch(methodInfo, GetPatch("AssetBundlePatch"));
            }

            MelonCoroutines.Start(CreditWorker());
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(e.Data);
            Console.ResetColor();
        }

        private IEnumerator CreditWorker()
        {
            while (_queue != null)
            {
                try
                {
                    if (_queue.Count != 0 && RoomManager.field_Internal_Static_ApiWorld_0 != null)
                    {
                        var __0 = _queue.Peek();
                        _queue.Dequeue();

                        var obj = new ExpandoObject() as IDictionary<String, object>;
                        foreach (PropertyDescriptor _ in TypeDescriptor.GetProperties(__0))
                        {
                            if (!_.Name.StartsWith("_"))
                            {
                                if (_.GetValue(__0) == null) _.SetValue(__0, "null");
                                obj[_.Name] = _.GetValue(__0);
                            }
                        }

                        obj["hash"] = BitConverter.ToString(Encoding.UTF8.GetBytes($"{__0.id}|{__0.assetUrl}|{__0.imageUrl}|{APIUser.CurrentUser.id}"));
                        _broadcaster.SendToChannel("AvatarChannel", JsonConvert.SerializeObject(obj, Formatting.None, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error while sending Avatar to API: " + e);
                }

                yield return new WaitForSeconds(1f);
            }
        }

        private static void AssetBundlePatch(ApiAvatar __0)
        {
            try { if (__0 != null && !cache.Contains(__0.id) && (APIUser.CurrentUser.id != __0.authorId)) { cache.Add(__0.id); _queue.Enqueue(__0); } } catch { }
        }
    }
}
