using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Extractor {
    class Program {
        public static void Main() {
            Console.WriteLine("Enter path to extract:");

            string path = Console.ReadLine();

            FileAttributes attr = File.GetAttributes(path);

            Directory.CreateDirectory("output");
            
            ThreadPool.SetMaxThreads(20, 0);

            if((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                string[] files = Directory.GetFiles(path);

                for(int index = 0; index < files.Length; index++) {
                    string file = files[index];

                    //Extract(file);

                    ThreadPool.QueueUserWorkItem(result => Extract(file));
                }
            }
            else
                Extract(path);

            Main();
        }

        public static void Extract(string file) {
            try {
                string name = Path.GetFileNameWithoutExtension(file);
                string nameExtension = Path.GetFileName(file);

                Console.WriteLine("Starting extraction process for " + nameExtension + "...");

                string output = "output/" + name;
                string outputImages = output + "/images";
                string outputManifest = output + "/manifest";

                if(Directory.Exists(output)) {
                    Console.WriteLine("Output directory already exists!");

                    //Directory.Delete(output, true);

                    return;
                }

                Directory.CreateDirectory(output);
                Directory.CreateDirectory(outputImages);
                Directory.CreateDirectory(outputManifest);

                string library = name;

                for(int character = 0; character < library.Length; character++) {
                    if(Char.IsLetter(library[character])) {
                        library = name.Substring(character);

                        break;
                    }
                }



                JObject manifest = ExtractFlash(library, name, file, output);


                using (StreamWriter writer = File.CreateText(output + "/" + name + ".json")) {
                    JsonSerializer serializer = new JsonSerializer();
                    
                    serializer.Serialize(writer, manifest);
                }

                Directory.Delete(outputImages, true);
                Directory.Delete(outputManifest, true);
            }
            catch(Exception exception) {
                Console.WriteLine(exception.Message);
                Console.WriteLine(exception.StackTrace);
            }
        }

        public static JObject ExtractFlash(string library, string libraryFull, string file, string output) {
            using(Process process = new Process()) {
                process.StartInfo = new ProcessStartInfo("cmd.exe", @"/c ffdec\ffdec.bat -swf2xml " + file + " " + output + "/manifest/flash.xml") {
                    RedirectStandardError = true,
                    RedirectStandardOutput = false
                };

                process.Start();

                process.WaitForExit();

                process.Close();
            }

            using(Process process = new Process()) {
                process.StartInfo = new ProcessStartInfo("cmd.exe", @"/c ffdec\ffdec.bat -export image " + output + "/images " + file) {
                    RedirectStandardError = true,
                    RedirectStandardOutput = false
                };

                process.Start();

                process.WaitForExit();

                process.Close();
            }

            using(Process process = new Process()) {
                process.StartInfo = new ProcessStartInfo("cmd.exe", @"/c ffdec\ffdec.bat -export binaryData " + output + "/manifest " + file) {
                    RedirectStandardError = true,
                    RedirectStandardOutput = false
                };

                process.Start();

                process.WaitForExit();

                process.Close();
            }

            //C:\Cortex\Utilities\Extractor\input\PRODUCTION-201701242205-837386173\dcr\hof_furni\15pillow.swf
            //C:\Cortex\Utilities\Extractor\input\PRODUCTION-201701242205-837386173\dcr\hof_furni\wooden_screen.swf

            string[] files = Directory.GetFiles(output, "*.*", SearchOption.AllDirectories);

            for(int index = 0; index < files.Length; index++) {
                string name = Path.GetFileName(files[index]);
                string path = Path.GetDirectoryName(files[index]);

                string newName = name.Substring(name.IndexOf('_') + 1).Replace(library + "_", "");

                Console.WriteLine("\t Rename " + name + " to " + newName);

                File.Move(files[index], path + "/" + newName);
            }

            JObject manifest = new JObject();

            string[] manifests = Directory.GetFiles(output, "*.bin", SearchOption.AllDirectories);

            for(int index = 0; index < manifests.Length; index++) {
                string name = Path.GetFileNameWithoutExtension(manifests[index]);

                Console.WriteLine("reading " + manifests[index]);
                
                XmlDocument document = new XmlDocument();
                
                document.Load(manifests[index]);

                string documentString = JsonConvert.SerializeXmlNode(document);

                manifest[name] = JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(document.FirstChild.NextSibling).Replace("@", ""));
            }

            using(Process process = new Process()) {
                string fuckOff = "/c TexturePacker.exe --trim-sprite-names --alpha-handling KeepTransparentPixels --max-width 4048 --max-height 4048 --disable-rotation --trim-mode None --disable-auto-alias --png-opt-level 0 --algorithm Basic --extrude 0 --format json --data C:/Cortex/Utilities/Extractor/output/" + libraryFull + "/" + libraryFull + ".json C:/Cortex/Utilities/Extractor/output/" + libraryFull + "/images/";

                process.StartInfo = new ProcessStartInfo("cmd.exe", fuckOff) {
                    WorkingDirectory = "C:/Program Files/CodeAndWeb/TexturePacker/bin/"
                };

                process.Start();

                process.WaitForExit();

                process.Close();
            }

            //XmlDocument sprites = new XmlDocument();

            //sprites.Load(output + "/" + libraryFull + ".xml");

            Dictionary<string, Dictionary<string, string>> dictionary = new Dictionary<string, Dictionary<string, string>>();

            JObject sprites = JObject.Parse(File.ReadAllText(output + "/" + libraryFull + ".json"));

            JToken frames = sprites["frames"];

            foreach(var item in (JObject)frames) {
                Dictionary<string, string> properties = new Dictionary<string, string>();

                properties.Add("left", item.Value["frame"]["x"].ToString());
                properties.Add("top", item.Value["frame"]["y"].ToString());
                properties.Add("width", item.Value["frame"]["w"].ToString());
                properties.Add("height", item.Value["frame"]["h"].ToString());

                dictionary.Add(libraryFull + "_" + item.Key, properties);
            }

            XmlDocument flash = new XmlDocument();

            flash.Load(output + "/manifest/flash.xml");

            XmlNode node = flash.SelectSingleNode("//item[@type='SymbolClassTag']");

            XmlNode nodeTags = node.SelectSingleNode("tags");
            XmlNode nodeNames = node.SelectSingleNode("names");

            Dictionary<int, int> tagIndexes = new Dictionary<int, int>();

            int tagIndex = 0;

            foreach(XmlNode tag in nodeTags.SelectNodes("item")) {
                tagIndex++;

                tagIndexes.Add(tagIndex, Int32.Parse(tag.InnerText));
            }

            Dictionary<int, string> tagNames = new Dictionary<int, string>();

            tagIndex = 0;

            Dictionary<int, string> tagNamesMissing = new Dictionary<int, string>();

            foreach(XmlNode name in nodeNames.SelectNodes("item")) {
                tagIndex++;

                string innerText = name.InnerText.Replace(library + "_" + library, library);

                if(!innerText.Any(char.IsDigit) && !innerText.Contains("icon")) {
                    Console.WriteLine("\t\tNotice: skipping " + innerText + " due to probable binary file!");

                    continue;
                }

                if(!dictionary.ContainsKey(innerText)) {
                    tagNamesMissing.Add(tagIndex, innerText);

                    continue;
                }

                int tag = tagIndexes[tagIndex];

                if(tagNames.ContainsKey(tag))
                    continue;

                tagNames.Add(tag, innerText);
                
                Console.WriteLine("\tTag " + tag + " is " + innerText);
            }

            foreach(KeyValuePair<int, string> missing in tagNamesMissing) {
                if(!tagIndexes.ContainsKey(missing.Key)) {
                    Console.WriteLine("\t\tWarning: tag " + missing.Key + " wasn't found!");

                    continue;
                }

                Console.WriteLine("\t" + missing.Value + " uses tag " + missing.Key);

                if(!tagNames.ContainsKey(tagIndexes[missing.Key])) {
                    Console.WriteLine("\t\tError: tag " + missing.Key + " doesn't exist!");

                    continue;
                }

                if(!dictionary.ContainsKey(missing.Value)) {
                    Console.WriteLine("\t\tWarning: " + missing.Value + " did not have a prior asset statement!");

                    dictionary.Add(missing.Value, new Dictionary<string, string>());
                }

                dictionary[missing.Value].Add("link", tagNames[tagIndexes[missing.Key]]);
            }

            manifest["sprites"] = JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(dictionary).Replace("@", ""));

            return manifest;
        }
    }
}
