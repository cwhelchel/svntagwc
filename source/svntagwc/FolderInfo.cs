using System.Collections.Generic;
using System;

namespace svntagwc
{
    class FolderInfo
    {
        private List<SvnExternal> externals = new List<SvnExternal>();
        private string svnFolderProps;


        public string FolderPath { get; set; }
        public string FrozenExtFilename { get; set; }

        public string FolderProperty
        {
            get
            {
                return svnFolderProps;
            }
            set
            {
                svnFolderProps = value;
                DecodeExternals(svnFolderProps);
            }
        }

        public List<SvnExternal> FolderExternals
        {
            get { return externals; }
            set { externals = value; }
        }

        internal void DecodeExternals(string fullExternalList)
        {
            string[] xs = fullExternalList.Trim().Split('\r','\n');

            foreach (string x in xs)
            {
                externals.Add(new SvnExternal(x));
            }
        }
    }
}