using LibGit2Sharp;
using System.Text.RegularExpressions;

namespace GithookPreCommit
{
    /// <summary>
    /// $Id$ 
    /// 
    /// Documentation: https://www.codeproject.com/Articles/1161290/Save-Yourself-Some-Troubles-with-TortoiseGit-Pre-c
    /// </summary>

    class Program
    {
        private static string COMMIT_ID_MARKER = string.Format("{0}Id{0}", "$");
        private static string COMMIT_ID_MARKER_EXPRESSION_PATTERN = string.Format(@"({0}Id(.*?){0})", @"\$");
        private static string NOT_FOR_REPO_MARKER = string.Format("{0}NotForRepo{0}", "$");
        private static Regex NotForRepoMarkerExpression = new(NOT_FOR_REPO_MARKER.Replace("$", @"\$", StringComparison.InvariantCultureIgnoreCase));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                string[] affectedPaths = File.ReadAllLines(args[0]);
                foreach (string path in affectedPaths)
                {
                    if (!ShouldFileBeChecked(path))
                    {
                        Log($"{path} is skipped.");
                        continue;
                    }

                    if (HasNotForRepoMarker(path))
                    {
                        string errorMessage = $"{NOT_FOR_REPO_MARKER} marker found in {path}";
                        Log(errorMessage);
                        Console.Error.WriteLine(errorMessage);
                        Environment.Exit(1);
                    }

                    if (!ReplaceCommitIdMarkerIfExists(path))
                    {
                        string errorMessage = $"{COMMIT_ID_MARKER} marker in {path} could not be replaced";
                        Log(errorMessage);
                        Console.Error.WriteLine(errorMessage);
                        Environment.Exit(1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error has occured while reading the files that need to be committed", ex);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static bool ShouldFileBeChecked(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                Regex filePattern = new(@"^.*\.(cs|java|sql|hsc)$", RegexOptions.IgnoreCase);
                return File.Exists(path) && filePattern.IsMatch(path);
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error has occured while reading the file in {path}";
                Log($"{errorMessage}: {ex.Message} ({ex.InnerException?.Message ?? string.Empty})");
                Console.Error.WriteLine(errorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static bool HasNotForRepoMarker(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using StreamReader reader = File.OpenText(path);
                string? line = reader.ReadLine();
                while (!string.IsNullOrWhiteSpace(line))
                {
                    if (NotForRepoMarkerExpression.IsMatch(line))
                    {
                        return true;
                    }

                    line = reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error has occured while checking for {NOT_FOR_REPO_MARKER} marker in {path}";
                Log($"{errorMessage}: {ex.Message} ({ex.InnerException?.Message ?? string.Empty})");
                Console.Error.WriteLine(errorMessage, ex);
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static bool ReplaceCommitIdMarkerIfExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                string? repositoryPath = GetRepositoryPath();
                Log($"RepositoryPath = {repositoryPath ?? string.Empty}");
                if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
                {
                    return false;
                }

                string? commitId = GetCommitId(repositoryPath) ?? string.Empty;
                string currentFileVersion = File.ReadAllText(path);
                string newFileVersion = Regex.Replace(currentFileVersion, COMMIT_ID_MARKER_EXPRESSION_PATTERN, commitId);
                if (!newFileVersion.Equals(currentFileVersion))
                {
                    File.WriteAllText(path, newFileVersion);
                }
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error has occured while replacing {COMMIT_ID_MARKER} marker in {path}";
                Log($"{errorMessage}: {ex.Message} ({ex.InnerException?.Message ?? string.Empty})");
                Console.Error.WriteLine(errorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        static string? GetRepositoryPath()
        {
            string? workingDirectory = Environment.CurrentDirectory;
            workingDirectory = (!string.IsNullOrWhiteSpace(workingDirectory) && workingDirectory.EndsWith(@"\\"))
                ? workingDirectory.Remove(workingDirectory.Length - 1)
                : workingDirectory;
            Log($"WorkingDirectory = {workingDirectory ?? string.Empty}");
            return (!string.IsNullOrWhiteSpace(workingDirectory))
                ? $@"{workingDirectory}\.git"
                : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repositoryPath"></param>
        /// <returns></returns>
        static string? GetCommitId(string repositoryPath)
        {
            using var repo = new Repository(repositoryPath);
            Commit? headCommit = repo.Head.Commits.FirstOrDefault();
            string? commitId = headCommit?.Id.Sha;
            string? commitAuthor = headCommit?.Author.Name;
            string? commitCommitter = headCommit?.Committer.Name;
            DateTime? commitCommitterWhen = headCommit?.Committer.When.DateTime;
            return string.Format($"{0}Id: {1} {2} {3} {4:o} (prev commit) {0}",
                "$", commitId, commitAuthor, commitCommitter, commitCommitterWhen);
        }

        static void Log(string logMessage)
        {
            using StreamWriter writer = File.AppendText("GithookPreCommit.log");
            writer.WriteLine($"{DateTime.Now.ToLongDateString()} {DateTime.Now.ToLongTimeString()} - {logMessage}");
        }
    }
}