using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using HtmlAgilityPack;

namespace LiCo
{
    public enum LicenseType
    {
        File,
        ThirdPartyFile,
        Url,
        Expression
    }
    public class License : IEquatable<License>
    {
        public static License GetLicense(LicenseType type, string value)
        {
            var key = new LicenseCache.LicenseIdentifier(type, value);
            if (LicenseCache.Licenses.TryGetValue(key, out var package))
                return package;
            try
            {
                var l = new License(type, value);
                LicenseCache.Licenses.Add(key, l);
                return l;
            }
            catch (WebException)
            {
                Console.WriteLine($"warning: License not found at: {value}");
            }
            return null;
        }

        private string DownloadUrlAsText(Uri uri) => DownloadUrlAsText(uri, node => node);
        private string DownloadUrlAsText(Uri uri, Func<HtmlNode, HtmlNode> nodeSelector)
        {
            var wc = (HttpWebRequest) WebRequest.Create(uri);
            wc.Accept = "plain/text";

            using var resp = wc.GetResponse() as HttpWebResponse;
            using var respStream = resp.GetResponseStream();
            if (!resp.ContentType.Contains("plain/text"))
            {
                using var buffer = new MemoryStream();
                respStream.CopyTo(buffer);
                var hd = new HtmlDocument();
                buffer.Position = 0;
                var encoding = hd.DetectEncoding(buffer, true);
                buffer.Position = 0;
                if (encoding != null)
                    hd.Load(buffer, encoding);
                else if (resp.CharacterSet != null)
                    hd.Load(buffer, Encoding.GetEncoding(resp.CharacterSet));
                else
                    hd.Load(buffer);
                return HtmlToText.ConvertNode(nodeSelector(hd.DocumentNode));
            }
            else
            {
                var enc = (resp.CharacterSet != null ? Encoding.GetEncoding(resp.CharacterSet) : Encoding.UTF8);
                using var reader = new StreamReader(respStream, enc);
                return reader.ReadToEnd();
            }
        }

        private License(LicenseType type, string value)
        {
            LicenseType = type;
            LicenseValue = value;
            switch (type)
            {
                case LicenseType.ThirdPartyFile:
                case LicenseType.File:
                    LicenseText = value;
                    break;
                case LicenseType.Url:
                {
                    var uri = new Uri(value);
                    if (string.Compare(uri.Host, "github.com", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        string tmp = $"{uri.Scheme}://raw.githubusercontent.com";
                        bool foundBlob = false;
                        for(int i = 0; i < uri.Segments.Length; i++)
                        {
                            var seg = uri.Segments[i];
                            if (!foundBlob && seg == "blob/")
                            {
                                foundBlob = true;
                                continue;
                            }

                            tmp += seg;
                        }

                        uri = new Uri(tmp);
                    }

                    LicenseText = DownloadUrlAsText(uri);

                    break;
                }
                case LicenseType.Expression:
                {
                    var uri = new Uri($"https://spdx.org/licenses/{value}.html");
                    LicenseText = DownloadUrlAsText(uri, 
                        node =>
                        {
                            var obj = node.CreateNavigator()?.SelectSingleNode("//*[@property='spdx:licenseText']") as HtmlNodeNavigator;
                            if (obj != null)
                                return obj.CurrentNode;
                            
                            return node;
                        });
                    break;
                }
            }
        }
        public LicenseType LicenseType { get; }
        public string LicenseValue { get; }
        public string LicenseText { get; }

        public bool Equals(License other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return LicenseType == other.LicenseType && LicenseValue == other.LicenseValue;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((License) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int) LicenseType, LicenseValue);
        }
    }
}