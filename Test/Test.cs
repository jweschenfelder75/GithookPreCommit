using LibGit2Sharp;
using System.Text.RegularExpressions;

namespace GithookPreCommit
{
    /// <summary>
    /// $Id: 70abbe56d674e1d43e8a21280f9308a90709e077 byte2702 byte2702 2024-09-22T16:58:44.0000000+02:00 (prev commit) $ 
    /// 
    /// Documentation: https://www.codeproject.com/Articles/1161290/Save-Yourself-Some-Troubles-with-TortoiseGit-Pre-c
    /// </summary>

    class ProgramTest
    {
        private static string COMMIT_ID_MARKER = string.Format("{0}Id{0}", "$");
        private static string COMMIT_ID_MARKER_EXPRESSION_PATTERN = string.Format(@"({0}Id(.*?){0})", @"\$");
        private static string NOT_FOR_REPO_MARKER = string.Format("{0}NotForRepo{0}", "$");
        private static Regex NotForRepoMarkerExpression = new(NOT_FOR_REPO_MARKER.Replace("$", @"\$", StringComparison.InvariantCultureIgnoreCase));

        /// <summary>
        /// Entry point GitHook for TortoiseGit
        /// </summary>
        /// <param name="args">files provided by TortoiseGit</param>
        static void Main2(string[] args)
        {
            try
            {
                // Iterates through all files that shall be commited.
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
        /// Identifies by file extension C, JAVA, SQL or CS if a given file should be checked.
        /// </summary>
        /// <param name="path">file path</param>
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
        /// Checks if a given file has a NotForRepo marker. If yes, the file will not be uploaded and the Githook will throw an error message.
        /// </summary>
        /// <param name="path">file path</param>
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
        /// Checks if a given file has a NotForRepo marker. If yes, the file will not be uploaded and the Githook will throw an error message.
        /// </summary>
        /// <param name="path">file path</param>
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
                if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
                {
                    return false;
                }

                string? commitId = GetCommitId(repositoryPath) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(commitId))
                {
                    return false;
                }

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
        /// Gets the Git repository path from the working path.
        /// </summary>
        /// <returns>Git repository path</returns>
        static string? GetRepositoryPath()
        {
            string? workingDirectory = null;
            try
            {
                workingDirectory = Environment.CurrentDirectory;
                workingDirectory = (!string.IsNullOrWhiteSpace(workingDirectory) && workingDirectory.EndsWith(@"\\"))
                    ? workingDirectory.Remove(workingDirectory.Length - 1)
                    : workingDirectory;
                return (!string.IsNullOrWhiteSpace(workingDirectory))
                    ? $@"{workingDirectory}\.git"
                    : null;
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error has occured while retrieving Git repository path from working directory {workingDirectory ?? string.Empty}";
                Log($"{errorMessage}: {ex.Message} ({ex.InnerException?.Message ?? string.Empty})");
                Console.Error.WriteLine(errorMessage, ex);
                return null;
            }
        }

        /// <summary>
        /// Gets latest Git commit information from HEAD for the given Git repository path.
        /// </summary>
        /// <param name="repositoryPath">Git repository path</param>
        /// <returns></returns>
        static string? GetCommitId(string repositoryPath)
        {
            try
            {
                using var repo = new Repository(repositoryPath);
                Commit? headCommit = repo.Head.Commits.FirstOrDefault();
                string? commitId = headCommit?.Id.Sha;
                string? commitAuthor = headCommit?.Author.Name;
                string? commitCommitter = headCommit?.Committer.Name;
                DateTimeOffset commitCommitterWhen = headCommit?.Committer.When ?? DateTimeOffset.Now;
                string result = string.Format("{0}Id: {1} {2} {3} {4} (prev commit) {0}",
                    "$", commitId, commitAuthor, commitCommitter, commitCommitterWhen.ToString("o"));
                return result;
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error has occured while retrieving latest Git commit information for HEAD in {repositoryPath}";
                Log($"{errorMessage}: {ex.Message} ({ex.InnerException?.Message ?? string.Empty})");
                Console.Error.WriteLine(errorMessage, ex);
                return null;
            }
        }

        /// <summary>
        /// Logs a given message in the log file GithookPreCommit.log.
        /// </summary>
        /// <param name="logMessage"></param>
        static void Log(string logMessage)
        {
            try
            {
                using StreamWriter writer = File.AppendText("GithookPreCommit.log");
                writer.WriteLine($"{DateTime.Now.ToLongDateString()} {DateTime.Now.ToLongTimeString()} - {logMessage}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message, ex);
            }
        }
    }
}