using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace svntagwc
{
    class SvnExternal
    {
        public SvnExternal(string value)
        {
            var m = Regex.Match(value, @"\-r(\d+)");
            

            if (m.Success)
            {
                string rev = m.Groups[1].Value;

                try
                {
                    Revision = Convert.ToInt32(rev);
                    RevisionString = "-r" + Revision;
                }
                catch
                {
                    Revision = -1;
                    RevisionString = String.Empty;
                }

                // get the URL
                m = Regex.Match(value, @"(?<Protocol>\w+):\/\/(?<Domain>[\w@][\w.:@]+)\/?[\w\.?=%&=\-@/$,:]*");
                Url = m.ToString();

                // get local folder for external (should be first thing)
                var xs = value.Trim().Split(' ');
                LocalFolder = xs[0];
            }
            else
            {
                //var xs = value.Trim().Split(' ');
                var xs = Utils.RegexSplit(value.Trim(), @"\s+");
                LocalFolder = xs[0];
                Url = xs[1];
                Revision = -1;
                RevisionString = String.Empty;
            }
        }

        public void GetCurrentRevision()
        {
            if (Revision == -1)
            {
                // get it
                string s = Program.GetSvnInfoRev(Url);
                Revision = Convert.ToInt32(s);
                RevisionString = "-r" + Revision;
            }
        }

        public string LocalFolder { get; set; }
        public string Url { get; set; }
        public int Revision { get; set; }
        public string RevisionString { get; set; }

        public override string ToString()
        {
            return String.Format("{0} {1} {2}",LocalFolder, RevisionString, Url);
        }
    }
}
