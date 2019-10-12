using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using GTAServer.Console;
using GTAServer.Console.Modules;
using Microsoft.Extensions.Logging;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GTAServer.Console.Modules
{
    internal class VersionModule : IModule
    {
        public void OnEnable(ConsoleInstance instance)
        {
            if (!File.Exists("version")) return;

            var commit = ReadVersion(out var branch);
            var commitInfo = GetCommit(commit);

            if (commitInfo == null)
            {
                instance.Logger.LogWarning("Invalid server version, consider updating your server from gtacoop.com");
                return;
            }

            JArray commits = GetCommits(commitInfo.created_at.ToString(), branch);
            var commitCount = commits.Count(x => x.SelectToken("id").Value<string>() != commit);

            if (commitCount > 0)
            {
                instance.Logger.LogWarning("You are running an outdated version of GTAServer.core, "
                    + $"please consider updating on gtacoop.com (you are {commitCount} commits behind)");
            }
        }

        /// <summary>
        /// Gets a specific commit
        /// </summary>
        /// <param name="commit">The id of the commit</param>
        /// <returns>Commit info from gitlab api as dynamic object</returns>
        public dynamic GetCommit(string commit)
        {
            var request = WebRequest.Create(
                $"https://gitlab.com/api/v4/projects/2179833/repository/commits/{commit}");

            try
            {
                var response = request.GetResponse();
                var reader = new StreamReader(response.GetResponseStream());

                return JObject.Parse(reader.ReadToEnd());
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the last commits since date from master branch
        /// </summary>
        /// <param name="since">ISO 8601 data format since when commits should be get</param>
        /// <param name="branch">The branch you want to get commits from</param>
        /// <returns>Array of commits</returns>
        public JArray GetCommits(string since, string branch)
        {
            var request = WebRequest.Create(
                $"https://gitlab.com/api/v4/projects/2179833/repository/commits?since={since}&ref_name={branch}");

            try
            {
                var response = request.GetResponse();
                var reader = new StreamReader(response.GetResponseStream());

                return JArray.Parse(reader.ReadToEnd());
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the current server version from "version file"
        /// </summary>
        /// <param name="branch">The branch used in this version</param>
        public static string ReadVersion(out string branch)
        {
            var lines = File.ReadAllLines("version")
                // don't read empty or comment lines
                .Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("#")).ToList();

            branch = lines[1]; // assume branch is always on second line (if not modified by user)
            return lines[0];
        }

        public string Name => "Version module";

        public string Description =>
            "Checks current server commit and do a version check";
    }
}
