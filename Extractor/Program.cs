using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using MySql.Data.MySqlClient;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Extractor {
    class Program {
        public static void Main() {
            Console.WriteLine("Enter path to extract:");

            string path = Console.ReadLine();

            if(path == "badges") {
                Dictionary<string, int> entries = new Dictionary<string, int>();

                using MySqlConnection connection = new MySqlConnection("server=127.0.0.1;uid=root;database=cortex");
                connection.Open();

                string[] lines = File.ReadAllLines("C:/Cortex/Utilities/Extractor/input/external_flash_texts.ini");

                foreach(string line in lines) {
                    try {
                        if(!line.StartsWith("badge_name_"))
                            continue;

                        string[] parameters = line.Split('=');
                        
                        string id = parameters[0].Replace("badge_name_", "").Trim();

                        if(entries.ContainsKey(id)) {
                            Console.WriteLine("Skipping duplicate badge title of " + id + "...");

                            continue;
                        }

                        string title = (parameters.Length == 2)?(parameters[1].Trim()):(id);

                        using MySqlCommand command = new MySqlCommand("INSERT INTO badges (id, title) VALUES (@id, @title)", connection);

                        command.Parameters.AddWithValue("@id", id);
                        command.Parameters.AddWithValue("@title", title);

                        command.ExecuteNonQuery();

                        Console.WriteLine("Setting badge title of " + id + " to '" + title + "'...");

                        entries.Add(id, 1);
                    }
                    catch(Exception exception) {
                        Console.WriteLine(exception.Message);
                        Console.WriteLine(exception.StackTrace);
                    }
                }

                foreach(string line in lines) {
                    try {
                        if(!line.StartsWith("badge_desc_"))
                            continue;

                        string[] parameters = line.Split('=');
                        
                        string id = parameters[0].Replace("badge_desc_", "").Trim();

                        if(!entries.ContainsKey(id)) {
                            Console.WriteLine("Skipping badge description of " + id + "...");

                            continue;
                        }

                        string description = (parameters.Length == 2)?(parameters[1].Trim()):(id);

                        using MySqlCommand command = new MySqlCommand("UPDATE badges SET description = @description WHERE id = @id", connection);

                        command.Parameters.AddWithValue("@id", id);
                        command.Parameters.AddWithValue("@description", description);

                        command.ExecuteNonQuery();

                        Console.WriteLine("Setting badge description of " + id + " to '" + description + "'...");
                    }
                    catch(Exception exception) {
                        Console.WriteLine(exception.Message);
                        Console.WriteLine(exception.StackTrace);
                    }
                }

                Console.WriteLine("Finished with " + entries.Count + " entries!");

                Main();

                return;
            }
            else if(path == "shop") {
                string[] directories = Directory.GetDirectories("C:/Cortex/Client/assets/HabboFurnitures");

                for(int index = 0; index < directories.Length; index++) {
                    string line = Path.GetFileName(directories[index]);
    
                    long page = 0;

                    using(MySqlConnection connection = new MySqlConnection("server=127.0.0.1;uid=root;database=cortex")) {
                        connection.Open();

                        using(MySqlCommand command = new MySqlCommand("INSERT INTO shop (parent, title, `order`, icon, type) VALUES (2, @line, 0, 1, 'default')", connection)) {
                            command.Parameters.AddWithValue("@line", line);

                            command.ExecuteNonQuery();

                            page = command.LastInsertedId;
                        }
                    }

                    string[] furnitures = Directory.GetDirectories(directories[index]);

                    for(int furni = 0; furni < furnitures.Length; furni++) {
                        string asset = Path.GetFileName(furnitures[furni]);

                        using(MySqlConnection connection = new MySqlConnection("server=127.0.0.1;uid=root;database=cortex")) {
                            connection.Open();

                            using(MySqlCommand command = new MySqlCommand("INSERT INTO shop_furnitures (shop, furniture) VALUES (@page, @furniture)", connection)) {
                                command.Parameters.AddWithValue("@page", page);
                                command.Parameters.AddWithValue("@furniture", asset);

                                command.ExecuteNonQuery();
                            }
                        }

                        try {
                            JObject manifest = JObject.Parse(File.ReadAllText(furnitures[furni] + "/" + asset + ".json"));

                            string logic = manifest["index"]["object"]["logic"].ToString();
                            double depth = manifest["logic"]["objectData"]["model"]["dimensions"]["z"].ToObject<double>();

                            using(MySqlConnection connection = new MySqlConnection("server=127.0.0.1;uid=root;database=cortex")) {
                                connection.Open();

                                using(MySqlCommand command = new MySqlCommand("UPDATE furnitures SET logic = @logic, depth = @depth WHERE id = @id", connection)) {
                                    command.Parameters.AddWithValue("@logic", logic);
                                    command.Parameters.AddWithValue("@depth", depth);
                                    command.Parameters.AddWithValue("@id", asset);

                                    command.ExecuteNonQuery();
                                }
                            }
                        }
                        catch(Exception exception) {
                            Console.WriteLine(exception.Message);
                            Console.WriteLine(exception.StackTrace);
                        }
                    }
                }

                Main();

                return;
            }
            else if(path == "walls") {
                string[] directories = Directory.GetDirectories("C:/Cortex/Client/assets/HabboFurnitures");

                for(int index = 0; index < directories.Length; index++) {
                    string line = Path.GetFileName(directories[index]);

                    string[] furnitures = Directory.GetDirectories(directories[index]);

                    for(int furni = 0; furni < furnitures.Length; furni++) {
                        string asset = Path.GetFileName(furnitures[furni]);

                        try {
                            JObject manifest = JObject.Parse(File.ReadAllText(furnitures[furni] + "/" + asset + ".json"));

                            if(manifest["logic"]["objectData"]["model"]["dimensions"].SelectToken("centerZ") == null)
                                continue;

                            using(MySqlConnection connection = new MySqlConnection("server=127.0.0.1;uid=root;database=cortex")) {
                                connection.Open();

                                using(MySqlCommand command = new MySqlCommand("UPDATE furnitures SET type = 'wall' WHERE id = @id", connection)) {
                                    command.Parameters.AddWithValue("@id", asset);

                                    command.ExecuteNonQuery();
                                }
                            }
                        }
                        catch(Exception exception) {
                            Console.WriteLine(exception.Message);
                            Console.WriteLine(exception.StackTrace);
                        }
                    }
                }

                Main();

                return;
            }

            if(path.Split(' ')[0] == "pack") {
                Pack(path.Split(' ')[1]);

                Main();

                return;
            }

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

        public static void Pack(string file) {
            string name = Path.GetFileNameWithoutExtension(file);
            string nameExtension = Path.GetFileName(file);

            Console.WriteLine("Starting extraction process for " + nameExtension + "...");

            string output = "output/" + name;

            if(!Directory.Exists(output))
                Directory.CreateDirectory(output);

            using(Process process = new Process()) {
                string fuckOff = "/c TexturePacker.exe --trim-sprite-names --alpha-handling KeepTransparentPixels --max-width 4048 --max-height 4048 --disable-rotation --trim-mode None --disable-auto-alias --png-opt-level 0 --algorithm Basic --extrude 0 --format json --data C:/Cortex/Utilities/Extractor/output/" + name + "/" + name + ".json " + file;

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

            JObject sprites = JObject.Parse(File.ReadAllText(output + "/" + name + ".json"));

            JToken frames = sprites["frames"];

            foreach(var item in (JObject)frames) {
                Dictionary<string, string> properties = new Dictionary<string, string>();

                properties.Add("left", item.Value["frame"]["x"].ToString());
                properties.Add("top", item.Value["frame"]["y"].ToString());
                properties.Add("width", item.Value["frame"]["w"].ToString());
                properties.Add("height", item.Value["frame"]["h"].ToString());

                dictionary.Add(item.Key, properties);
            }

            using (StreamWriter writer = File.CreateText(output + "/" + name + ".json")) {
                JsonSerializer serializer = new JsonSerializer();
                
                serializer.Serialize(writer, dictionary);
            }
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


                if(nameExtension.EndsWith(".swf")) {
                    JObject manifest = ExtractFlash(library, name, file, output);


                    using (StreamWriter writer = File.CreateText(output + "/" + name + ".json")) {
                        JsonSerializer serializer = new JsonSerializer();
                        
                        serializer.Serialize(writer, manifest);
                    }

                    Directory.Delete(outputImages, true);
                    Directory.Delete(outputManifest, true);
                }
                else if(nameExtension.EndsWith(".xml")) {
                    if(name == "furnidata") {
                        ExtractFurnitures(file, output);
                    }
                    else {
                        XmlDocument document = new XmlDocument();
                        document.Load(file);
                        
                        using (StreamWriter writer = File.CreateText(output + "/" + name + ".json")) {
                            JsonSerializer serializer = new JsonSerializer();
                            
                            serializer.Serialize(writer, JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(document.FirstChild.NextSibling).Replace("@", "")));
                        }
                    }
                }
            }
            catch(Exception exception) {
                Console.WriteLine(exception.Message);
                Console.WriteLine(exception.StackTrace);
            }
        }

        [Flags]
        public enum GameFurnitureFlags {
            Stackable   = 1 << 0,
            Sitable     = 1 << 1,
            Standable   = 1 << 2,
            Walkable    = 1 << 3,
            Sleepable   = 1 << 4
        };

        public static void ExtractFurnitures(string file, string output) {
            XmlDocument document = new XmlDocument();

            document.Load(file);

            string[] directories = Directory.GetDirectories("C:/Cortex/Client/assets/HabboFurnitures");

            for(int index = 0; index < directories.Length; index++) {
                string name = Path.GetFileName(directories[index]);

                string library = name;

                for(int character = 0; character < library.Length; character++) {
                    if(Char.IsLetter(library[character])) {
                        library = name.Substring(character);

                        break;
                    }
                }

                Console.WriteLine("Restoring asset " + name + "...");

                try {
                    XmlNode node = null;

                    foreach(XmlNode child in document.SelectNodes("//roomitemtypes")) {
                        node = child.SelectSingleNode("//furnitype[starts-with(@classname, '" + library + "')]");

                        if(node == null)
                            continue;

                        break;
                    }

                    if(node == null) {
                        Console.WriteLine("FOUND NOTHING FOR " + library);

                        continue;
                    }

                    XmlNode subNode;

                    subNode = node.SelectSingleNode("defaultdir");
                    int direction = (subNode == null)?(2):(Int32.Parse(subNode.InnerText));
                    
                    subNode = node.SelectSingleNode("xdim");
                    double dimensionBreadth = (subNode == null)?(1):(Convert.ToDouble(subNode.InnerText));
                    
                    subNode = node.SelectSingleNode("ydim");
                    double dimensionHeight = (subNode == null)?(1):(Convert.ToDouble(subNode.InnerText));
                    
                    subNode = node.SelectSingleNode("name");
                    string title = (subNode == null)?("Unknown"):(subNode.InnerText);
                    
                    subNode = node.SelectSingleNode("description");
                    string description = (subNode == null)?("Unknown"):(subNode.InnerText);
                    
                    subNode = node.SelectSingleNode("customparams");
                    string parameters = (subNode == null)?(""):(subNode.InnerText);
                    
                    subNode = node.SelectSingleNode("furniline");
                    string furniline = (subNode == null)?("none"):(subNode.InnerText);

                    GameFurnitureFlags flags = 0;
                    
                    subNode = node.SelectSingleNode("canstandon");
                    if(subNode != null && Int32.Parse(subNode.InnerText) == 1) flags |= GameFurnitureFlags.Standable | GameFurnitureFlags.Walkable | GameFurnitureFlags.Stackable;
                    
                    subNode = node.SelectSingleNode("cansiton");
                    if(subNode != null && Int32.Parse(subNode.InnerText) == 1) flags |= GameFurnitureFlags.Sitable | GameFurnitureFlags.Walkable | GameFurnitureFlags.Stackable;
                    
                    subNode = node.SelectSingleNode("canlayon");
                    if(subNode != null && Int32.Parse(subNode.InnerText) == 1) flags |= GameFurnitureFlags.Sleepable | GameFurnitureFlags.Walkable | GameFurnitureFlags.Stackable;

                    string colors = "";

                    subNode = node.SelectSingleNode("partcolors");
                    if(subNode != null && subNode.HasChildNodes) {
                        colors = "";

                        foreach(XmlNode color in subNode.ChildNodes) {
                            if(colors.Length != 0)
                                colors += ",";

                            colors += color.InnerText;
                        }
                    }

                    using MySqlConnection connection = new MySqlConnection("server=127.0.0.1;uid=root;database=cortex");

                    connection.Open();

                    using MySqlCommand command = new MySqlCommand("INSERT INTO furnitures (id, line, title, description, flags, breadth, height, depth, direction, parameters, colors) VALUES (@id, @line, @title, @description, @flags, @breadth, @height, @depth, @direction, @parameters, @colors)", connection);

                    command.Parameters.AddWithValue("@id", name);
                    command.Parameters.AddWithValue("@line", furniline);
                    command.Parameters.AddWithValue("@title", title);
                    command.Parameters.AddWithValue("@description", description);
                    command.Parameters.AddWithValue("@flags", flags);
                    command.Parameters.AddWithValue("@breadth", dimensionBreadth);
                    command.Parameters.AddWithValue("@height", dimensionHeight);
                    command.Parameters.AddWithValue("@depth", 0);
                    command.Parameters.AddWithValue("@direction", direction);
                    command.Parameters.AddWithValue("@parameters", parameters);
                    command.Parameters.AddWithValue("@colors", colors);

                    command.ExecuteNonQuery();

                    long row = command.LastInsertedId;

                    
                    Console.WriteLine("Inserted furniture row with row index " + row + "!");

                    if(!Directory.Exists("C:/Cortex/Client/assets/HabboFurnitures/" + furniline))
                        Directory.CreateDirectory("C:/Cortex/Client/assets/HabboFurnitures/" + furniline);

                    Directory.Move(directories[index], "C:/Cortex/Client/assets/HabboFurnitures/" + furniline + "/" + name);
                }
                catch(Exception exception) {
                    Console.WriteLine(exception.Message);
                    Console.WriteLine(exception.StackTrace);
                }
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
