using System;
using System.Xml;
using System.IO;

namespace Sassifier {
    class Program {
        static void Main(string[] args) {
            string directory = "";

            if(args.Length == 0) {
                Console.WriteLine("Enter document path:");

                directory = Console.ReadLine();
            }
            else
                directory = args[0];

            Console.WriteLine("Reading " + directory + @"\Sprites.xml");

            XmlDocument document = new XmlDocument();

            document.Load(directory + @"\Sprites.xml");

            XmlNode atlas = document.SelectSingleNode("TextureAtlas");

            string styles = "$url: url('./../images/Sprites/Sprites.png');\r\n";

            foreach(XmlNode sprite in atlas.SelectNodes("sprite")) {
                string name = sprite.Attributes["n"].Value;

                string left = sprite.Attributes["x"].Value;
                string top = sprite.Attributes["y"].Value;

                string width = sprite.Attributes["w"].Value;
                string height = sprite.Attributes["h"].Value;

                string style = "@mixin sprite-" + name + " { background: $url -" + left + "px -" + top + "px; width: " + width + "px; height: " + height + "px; }";

                Console.WriteLine(style);

                styles += "\r\n" + style;

                styles += "\r\n.sprite-" + name + " { position: relative; width: " + width + "px; height: " + height + "px; &:after { content: ''; position: absolute; left: 0; top: 0; @include sprite-" + name + "(); } }\r\n";
            }

            File.WriteAllText(directory + @"\Sprites.scss", styles);

            Console.ReadKey();
        }
    }
}
