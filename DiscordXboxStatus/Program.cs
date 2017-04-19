using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Net.Http.Headers;
using DiscordXboxStatus.Properties;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace DiscordXboxStatus {
    class Program {
        [DllImport("user32.dll")]
        static extern int SetWindowText(IntPtr hWnd, string text);

        static object running = true;

        static void Main(string[] args) {
             if(args.Length > 0) {
                Console.CancelKeyPress += Console_CancelKeyPress;
                Console.WriteLine("Launching pretend " + string.Join(" ", args));
                // Child (game status) process
                SetWindowText(Process.GetCurrentProcess().MainWindowHandle, string.Join(" ", args) + " (Xbox)");

                while (true) Thread.Sleep(5000);
            } else {
                // Parent (polling) process
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-AUTH", Resources.XBOXAPI_KEY);
                string xuidResult = client.GetAsync("https://xboxapi.com/v2/accountxuid").Result.Content.ReadAsStringAsync().Result;
                JToken xuid = JsonConvert.DeserializeObject<JToken>(xuidResult);

                string presenceResult;
                JToken presence;
                Dictionary<string, Process> processes = new Dictionary<string, Process>(), toAdd = new Dictionary<string, Process>();
                Dictionary<string, bool> currentGames = new Dictionary<string, bool>();
                IEnumerable<string> keys;
                while ((bool)running) {
                    Console.WriteLine("Polling...");
                    presenceResult = client.GetAsync("https://xboxapi.com/v2/" + xuid["xuid"].ToString() + "/presence").Result.Content.ReadAsStringAsync().Result;
                    presence = JsonConvert.DeserializeObject<JToken>(presenceResult);

                    if(presence["state"].ToString() == "Online") {
                        Console.WriteLine("State is online");
                        foreach(JToken device in presence["devices"] as JArray) {
                            foreach(JToken title in device["titles"] as JArray) {
                                if (title["name"].ToString() == "Xbox App" || title["name"].ToString() == "Home") continue;
                                currentGames[title["name"].ToString()] = true;
                                Console.WriteLine("Adding new game " + title["name"].ToString());
                            }
                        }
                    }

                    keys = currentGames.Keys.Select(item => (string)item.Clone()).ToList();
                    foreach (string game in keys) {
                        if (currentGames[game] && !processes.ContainsKey(game)) {
                            Console.WriteLine("Launching process for game " + game);
                            processes[game] = LaunchGameProcess(game);
                        } else if(currentGames[game] == false) {
                            Console.WriteLine("Closing process for game " + game);
                            processes[game].CloseMainWindow();
                        }
                        currentGames[game] = false;
                    }

                    Thread.Sleep(30000);
                }
            }
        }

        private static Process LaunchGameProcess(string name) {
            string processName = Directory.GetCurrentDirectory() + "/" + name + ".exe";
            if (!File.Exists(processName)) {
                File.Copy(System.Reflection.Assembly.GetEntryAssembly().Location, processName);
            }
            ProcessStartInfo psi = new ProcessStartInfo(processName, name);
            Process p = Process.Start(psi);
            return p;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e) {
            lock (running) {
                Monitor.Pulse(running);
                running = false;
            }
        }
    }
}
