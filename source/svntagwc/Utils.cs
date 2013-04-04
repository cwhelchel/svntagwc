using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace svntagwc
{
    class Utils
    {
        private static bool useAuth = false;
        private static string userName = String.Empty;
        private static string userPass = String.Empty;

        public static void SetSvnAuth(string user, string pass)
        {
            useAuth = true;
            userName = user;
            userPass = pass;
        }

        public static string GetFileContents(string path)
        {
            string fileContents;
            var helpFilePath = path;

            if (File.Exists(helpFilePath))
                using (var sr = new StreamReader(helpFilePath))
                {
                    fileContents = sr.ReadToEnd();
                }
            else
                fileContents = "File not found";

            return fileContents;
        }

        public static string GetUrlDomain(string url)
        {
            string results = string.Empty;
            var match = Regex.Match(url, @"^http[s]?[:/]+[^/]+");
            if (match.Success)
                results = match.Captures[0].Value;

            return results;
        }

        public static bool DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                try
                {
                    ClearAttributes(path);
                    Directory.Delete(path, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            return true;
        }

        public static void CreateDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
        }

        public static void ClearAttributes(string currentDir)
        {
            if (!Directory.Exists(currentDir)) return;

            var subDirs = Directory.GetDirectories(currentDir);
            foreach (var dir in subDirs)
                ClearAttributes(dir);

            var files = Directory.GetFiles(currentDir);
            foreach (var file in files)
                File.SetAttributes(file, FileAttributes.Normal);
        }

        public static string[] RegexSplit(string input, string pattern)
        {
            return Regex.Split(input, pattern)
              .Where(item => !string.IsNullOrEmpty(item)).ToArray();
        }

        public static string RegexReplace(string input, string pattern, string replacement)
        {
            return Regex.Replace(input, pattern, replacement);
        }

        public static bool PrintSvnCmd { get; set; }

        public static XmlReader SvnCommand(string format, params object[] args)
        {
            XmlReader xmlReader = null;
            try
            {
                var command = new Process();
                command.EnableRaisingEvents = false;
                command.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
                command.StartInfo.FileName = "svn.exe";

                string argument = String.Format(format, args);

                if (useAuth)
                    argument = String.Format(" {0} --username {1} --password {2} --non-interactive --no-auth-cache", argument, userName, userPass);

                if (PrintSvnCmd) 
                    Console.WriteLine("svn " + argument);

                command.StartInfo.Arguments = argument;
                command.StartInfo.UseShellExecute = false;
                command.StartInfo.RedirectStandardOutput = true;
                
                command.Start();
                command.WaitForExit();

                Stream svnStream = command.StandardOutput.BaseStream;
                xmlReader = XmlReader.Create(svnStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " = SVN.exe");
                Environment.Exit(0);
            }
            return xmlReader;
        }

        public static void SvnCommandBlock(string format, params object[] args)
        {
            try
            {
                var command = new Process();
                command.EnableRaisingEvents = false;
                command.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
                command.StartInfo.FileName = "svn.exe";

                string argument = String.Format(format, args);

                if (useAuth)
                    argument = String.Format("{0} --username {1} --password {2} --non-interactive --no-auth-cache", argument, userName, userPass);

                if (PrintSvnCmd)
                    Console.WriteLine("svn " + argument);

                command.StartInfo.UseShellExecute = false;
                command.StartInfo.Arguments = argument;

                command.Start();
                Console.WriteLine("this may take some time....");
                command.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " = SVN.exe");
                Environment.Exit(0);
            }
        }
    }
}