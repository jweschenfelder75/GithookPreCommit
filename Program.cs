using LibGit2Sharp;
using System.Text.RegularExpressions;

namespace GithookPreCommit
{
    /// <summary>
    /// $Id$
    /// </summary>

    class Program
    {
        private const string COMMIT_ID_MARKER = "$Id$";
        private const string COMMIT_ID_MARKER_EXPRESSION_PATTERN = @"(\\$Id(.*?)\\$)";
        private const string NOT_FOR_REPO_MARKER = "$NotForRepo$";

        private static Regex NotForRepoMarkerExpression => new(NOT_FOR_REPO_MARKER.Replace("$", @"\$", StringComparison.CurrentCultureIgnoreCase));

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
                    if (ShouldFileBeChecked(path))
                    {
                        continue;
                    }

                    if (HasNotForRepoMarker(path))
                    {
                        string errorMessage = $"{NOT_FOR_REPO_MARKER} marker found in {path}";
                        Console.Error.WriteLine(errorMessage);
                        Environment.Exit(1);
                    }

                    if (ReplaceCommitIdMarkerIfExists(path))
                    {
                        string errorMessage = $"{COMMIT_ID_MARKER} marker in {path} could not be replaced";
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
            if (path == null || !File.Exists(path))
            {
                return false;
            }

            try
            {
                Regex filePattern = new(@"^.*\.(cs|java|aql|hsc)$", RegexOptions.IgnoreCase);
                return File.Exists(path) && filePattern.IsMatch(path);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error has occured while reading the file in {path}", ex);
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
            if (path is null || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using StreamReader reader = File.OpenText(path);
                string? line = reader.ReadLine();
                while (line is not null)
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
                Console.Error.WriteLine($"An error has occured while checking for {NOT_FOR_REPO_MARKER} marker in {path}", ex);
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
            if (path is null || !File.Exists(path))
            {
                return false;
            }

            try
            {
                string? repositoryPath = GetRepositoryPath();
                if (repositoryPath is null || !Directory.Exists(repositoryPath))
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
                Console.Error.WriteLine($"An error has occured while replacing {COMMIT_ID_MARKER} marker in {path}", ex);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        static string? GetRepositoryPath()
        {
            string workingDirectory = Environment.CurrentDirectory;
            DirectoryInfo? rootDirectory = Directory.GetParent(workingDirectory)?.Parent;
            return (rootDirectory?.Parent is not null)
                ? $@"{rootDirectory?.Parent?.FullName}\.git"
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
            return $"$Id: {commitId} {commitAuthor} {commitCommitter} {commitCommitterWhen:o} (prev commit) $";
        }
    }
}