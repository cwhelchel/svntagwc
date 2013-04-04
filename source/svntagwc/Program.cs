using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NDesk.Options;
using System.Xml.Linq;
using System.Xml;
using System.IO;

namespace svntagwc
{
    class Program
    {
        static string wcPath = String.Empty;
        static string tagUrl = String.Empty;
        static string tagLogMsg = String.Empty;
        static string addDepth = "infinity";
        static bool interactive = true;
        static bool verbose = false;
        static bool addUnversioned = false;
        static string addPath = String.Empty;
        static bool ccnet = false;
        static string userName = String.Empty;
        static string userPass = String.Empty;

        /// <summary>
        /// Main thread of execution for the entire program.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        static void Main(string[] args)
        {
            bool showHelp = false;
            OptionSet opts = new OptionSet()
            { 
                { "wc=", "use the local working copy at the\n given {PATH}",                 path => wcPath = path },
                { "t|tag=", "copy the working copy to the given \nfully qualified {URL}" +
                            ". If --ccnet is set this is the base URL for the tag folder but\n" + 
                            "REVPROP must be available for svn:externals on the server side",   
                            url => tagUrl = url },
                { "f|force", "force yes to interactive user input",                             _ => interactive = false },
                { "a|add", "recursively add all unversioned files \nin the given working copy", _ => addUnversioned = true },
                { "d|add-depth=", "depth to pass to svn add.\n" + 
                                "Must be one of the following: " +
                                "\'infinity\', \'immediates\', \'files\', \'empty\'.\n" +
                                "(\'infinity\' is the default)",                              
                            depth => addDepth = depth},
                { "add-path=", "If specified along with -a, this path will indicate a top-level directory\n" +
                              "within the working copy to add files from. Only the unversioned files within\n" +
                              "this path will be added.\n",
                            p => addPath = p },
                { "m|message", "SVN log message to use when tagging",                         msg => tagLogMsg = msg },
                { "h|help", "show this help message",  _ => showHelp = true },
                { "v|verbose", "enables extra output", _ => verbose = true },
                { "c|ccnet", "use CruiseControl .Net variables", _ => ccnet = true }, // overrides the wc arg
                { "username:", "used for svn --username", user => userName = user },
                { "password:", "used for svn --password", pw => userPass = pw }
            };

            if (args.Length == 0)
                showHelp = true;

            try
            {
                opts.Parse(args);
            }
            catch (OptionException oe)
            {
                Console.WriteLine("failed with error:");
                Console.WriteLine(oe.Message);
                ShowHelp(opts);
                Environment.ExitCode = 1;
                return;
            }

            if (!String.IsNullOrEmpty(userName) && 
                !String.IsNullOrEmpty(userPass))
            {
                Utils.SetSvnAuth(userName, userPass);
            }

            if (showHelp)
            {
                ShowHelp(opts);
                Environment.ExitCode = 0;
            }
            else
            {
                MainProcessing();
            }

            if (interactive || !ccnet)
            {
                Console.WriteLine("Press enter key to end");
                Console.ReadLine();
            }
        }

        private static void MainProcessing()
        {
            if (ccnet)
            {
                UseCCNet();
            }
            else // main processing
            {
                string url = GetSvnUrlFromWc(wcPath);
                var folders = GetExternalsFromUrl(url);
                FreezeExternalRevs(folders);
                WriteExternalsToFile(folders);
                FreezeExternalsOnWorkingCopy(folders);

                if (addUnversioned)
                {
                    if (String.IsNullOrEmpty(addPath))
                        addPath = wcPath;
                    AddUnversionedFiles(addPath);
                }

                CopyWorkingCopyToTag(tagUrl);
                Environment.ExitCode = 0;
            }
        }

        private static bool UseCCNet()
        {
            // when used in a <publisher> block of the CCNet project config (a publisher is like a post build)
            // svntagwc should not do it's working copy stuff and instead just find an existing tag and freeze 
            // any externals it finds on it.
            // note the publisher approach may not work if --revprop isn't available on the SVN server's pre-revprop hook
            // so this will be setup to modify working copy properties and let ccnet commit them 
            // (this will work as a <task> after build tasks)

            wcPath = Environment.GetEnvironmentVariable("CCNetWorkingDirectory", EnvironmentVariableTarget.Process);
            VerbosePrint("Got CCNET envvar: CCNetWorkingDirectory = " + wcPath);

            string s = Environment.GetEnvironmentVariable("CCNetLabel", EnvironmentVariableTarget.Process);
            VerbosePrint("Got CCNET envvar: CCNetLabel = " + s);
            if (String.IsNullOrEmpty(wcPath))
            {
                Console.WriteLine("Error: environment variable CCNetWorkingDirectory is null or empty!");
                Environment.ExitCode = 1;
                return false;
            }

            string wcUrl = GetSvnUrlFromWc(wcPath);
            var folders = GetExternalsFromUrl(wcUrl);
            FreezeExternalRevs(folders);
            WriteExternalsToFile(folders);
            FreezeExternalsOnWorkingCopy(folders, wcPath);

            if (addUnversioned)
            {
                if (String.IsNullOrEmpty(addPath))
                    addPath = wcPath;
                AddUnversionedFiles(addPath);
            }

            Environment.ExitCode = 0;
            return true;
        }

        private static void ShowHelp(OptionSet opts)
        {
            Console.WriteLine("svntagwc:");
            Console.WriteLine("\tTags a given working copy and can optionally freeze");
            Console.WriteLine("\texternals found in the folders of the working copy.");
            Console.WriteLine("\tAlso can optionally add unversioned files and folders");
            Console.WriteLine("\tfound within the working copy and commit them to a tag");
            Console.WriteLine("\tor any other given SVN location.");
            Console.WriteLine();
            Console.WriteLine("svntagwc USAGE:");
            opts.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
        }

        private static string GetSvnUrlFromWc(string path)
        {
            XDocument infoDoc = GetSvnInfo(path);
            var url = (from el in infoDoc.Descendants("url")
                       select el.Value).SingleOrDefault();
            Console.WriteLine("Working copy url is {0}", url);
            return url;
        }

        private static IEnumerable<FolderInfo> GetExternalsFromUrl(string url)
        {
            // TODO: add infinite depth? [cmw]
            XmlReader externalProps = Utils.SvnCommand("propget --xml -R svn:externals {0}", url);
            var propsDoc = XDocument.Load(externalProps);

            var externals =
                from external in propsDoc.Descendants("target")
                where external.Element("property").Attribute("name").Value == "svn:externals"
                select new FolderInfo
                            {
                                FolderPath = external.Attribute("path").Value,
                                FolderProperty = external.Element("property").Value
                            };

            return externals.ToList();
        }

        private static void FreezeExternalRevs(IEnumerable<FolderInfo> folders)
        {
            Console.WriteLine();
            Console.WriteLine("===================================================");
            Console.WriteLine("Freezing floating revision numbers on externals...");
            Console.WriteLine("===================================================");

            foreach (FolderInfo folder in folders)
            {
                VerbosePrint("Freezing folder externals for: " + folder.FolderPath);
                foreach (SvnExternal external in folder.FolderExternals)
                {
                    external.GetCurrentRevision();
                    VerbosePrint(String.Format("{0}\r\n\tto REV {1}", external.Url, external.RevisionString));
                }
            }
        }

        private static void WriteExternalsToFile(IEnumerable<FolderInfo> folders)
        {
            Console.WriteLine();
            Console.WriteLine("===================================================");
            Console.WriteLine("Writing frozen externals to file...");
            Console.WriteLine("===================================================");

            foreach (var folder in folders)
            {
                string wcName = Utils.RegexReplace(folder.FolderPath, @"\w+://", ""); // remove http or https
                wcName = wcName.Replace('/', '.').Replace(':', '_');
                string file = String.Format(@".\{0}_frozen.txt", wcName);
                VerbosePrint("frozen ext file: " + file);
                folder.FrozenExtFilename = file;
                using (StreamWriter sw = new StreamWriter(file, false))
                {
                    foreach (SvnExternal external in folder.FolderExternals)
                    {
                        sw.WriteLine(external);
                    }
                }
            }
        }

        private static void FreezeExternalsOnWorkingCopy(IEnumerable<FolderInfo> folders)
        {
            Console.WriteLine();
            Console.WriteLine("===================================================");
            Console.WriteLine("freezing working copy...");
            Console.WriteLine("===================================================");

            foreach (FolderInfo folder in folders)
            {
                if (UserResponse("Freeze all externals on working copy of \r\n\t " + folder.FolderPath))
                    Utils.SvnCommand("propset svn:externals --file {0} {1}", folder.FrozenExtFilename, wcPath);
            }
        }

        private static void FreezeExternalsOnWorkingCopy(IEnumerable<FolderInfo> folders, string path)
        {
            Console.WriteLine();
            Console.WriteLine("===================================================");
            Console.WriteLine("freezing working copy...");
            Console.WriteLine("===================================================");

            foreach (FolderInfo folder in folders)
            {
                if (UserResponse("Freeze all externals on working copy of \r\n\t " + folder.FolderPath))
                    Utils.SvnCommand("propset svn:externals --file {0} {1}", folder.FrozenExtFilename, path);
            }
        }

        private static void AddUnversionedFiles(string path)
        {
            Console.WriteLine();
            Console.WriteLine("===================================================");
            Console.WriteLine("Adding unversioned files in working copy...");
            Console.WriteLine("===================================================");

            VerbosePrint("adding from path: " + path);

            if (UserResponse("Add all unversioned items to the working copy?"))
                Utils.SvnCommandBlock("add {0} --force --depth {1} --no-ignore", path + @"\*", addDepth);
        }

        private static void CopyWorkingCopyToTag(string url)
        {
            Console.WriteLine();
            Console.WriteLine("===================================================");
            Console.WriteLine(@"copying/tagging the working copy to tag...");
            Console.WriteLine("===================================================");

            // from the SVN manual http://svnbook.red-bean.com/en/1.0/re07.html :
            // Copy an item in your working copy to a URL in the repository (an immediate commit, so you must supply a commit message):
            //      $ svn copy near.txt file:///tmp/repos/test/far-away.txt -m "Remote copy."
            //      Committed revision 8.
            // this means that an SVN commit is not required for WC->URL copies
            if (UserResponse("Tag the working copy?"))
            {
                if (String.IsNullOrEmpty(url))
                    url = UserInput("enter the url to copy to");

                VerbosePrint("copying from working copy: \n\t" + wcPath);
                VerbosePrint("copying to url: \n\t" + url);

                if (String.IsNullOrEmpty(tagLogMsg))
                    tagLogMsg = UserInput("enter a SVN log message for the copy");

                Utils.PrintSvnCmd = true;
                Utils.SvnCommand("copy {0} {1} -m \"{2}\"", wcPath, url, tagLogMsg);
                Utils.PrintSvnCmd = false;
            }
        }

        #region Helper methods
        internal static string GetSvnInfoRev(string path)
        {
            var infoDoc = GetSvnInfo(path);
            return infoDoc.Descendants("commit").Attributes("revision").FirstOrDefault().Value;
        }

        private static XDocument GetSvnInfo(string path)
        {
            XmlReader info = Utils.SvnCommand("info --xml {0}", path);
            var infoDoc = XDocument.Load(info);
            return infoDoc;
        }

        private static void VerbosePrint(string line)
        {
            if (verbose)
                Console.WriteLine(line);
        }

        private static bool UserResponse(string question)
        {
            if (!interactive)
                return true;

            Console.Write(question + " ( Y/N ): ");
            string response = Console.ReadLine();

            if (response.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
            else if (response.Equals("N", StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
            else
                return UserResponse(question); // re-ask
        }

        private static string UserInput(string message)
        {
            Console.Write(message + ": ");
            return Console.ReadLine();
        }
        #endregion Helper methods
    }
}
