using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace JUnit.TestLogger
{
    [FriendlyName(FriendlyName)]
    [ExtensionUri(ExtensionUri)]
    class JUnitXmlTestLogger : ITestLoggerWithParameters
    {
        /// <summary>
        /// Uri used to uniquely identify the logger.
        /// </summary>
        public const string ExtensionUri = "logger://Microsoft/TestPlatform/JUnitXmlLogger/v1";

        /// <summary>
        /// Alternate user friendly string to uniquely identify the console logger.
        /// </summary>
        public const string FriendlyName = "junit";

        public const string LogFilePathKey = "LogFilePath";
        public const string EnvironmentKey = "Environment";

        private string outputFilePath;
        private string environmentOpt;

        private readonly object resultsGuard = new object();
        private List<TestResultInfo> results;
        private DateTime localStartTime;

        private static List<string> errorTypes = new List<string>
        {
            "Test Assembly Cleanup Failure",
            "Test Collection Cleanup Failure",
            "Test Class Cleanup Failure",
            "Test Case Cleanup Failure",
            "Test Cleanup Failure",
            "Test Method Cleanup Failure"
        };

        private static Dictionary<string, string> errorTypeKeyValuePair = new Dictionary<string, string>
        {
            {"Test Assembly Cleanup Failure", "assembly-cleanup"},
            {"Test Collection Cleanup Failure", "test-collection-cleanup"},
            {"Test Class Cleanup Failure", "test-class-cleanup" },
            {"Test Case Cleanup Failure", "test-case-cleanup"},
            {"Test Cleanup Failure", "test-cleanup"},
            {"Test Method Cleanup Failure", "test-method-cleanup"}
        };

        // Disabling warning CS0659: 'XunitXmlTestLogger.TestResultInfo' overrides Object.Equals(object o) but does not override Object.GetHashCode()
        // As this is a false alarm here.
#pragma warning disable 0659
        private class TestResultInfo
        {
            public readonly TestCase TestCase;
            public readonly TestOutcome Outcome;
            public readonly string AssemblyPath;
            public readonly string Type;
            public readonly string Method;
            public readonly string Name;
            public readonly TimeSpan Time;
            public readonly string ErrorMessage;
            public readonly string ErrorStackTrace;
            public readonly Collection<TestResultMessage> Messages;
            public readonly TraitCollection Traits;

            public TestResultInfo(
                TestCase testCase,
                TestOutcome outcome,
                string assemblyPath,
                string type,
                string method,
                string name,
                TimeSpan time,
                string errorMessage,
                string errorStackTrace,
                Collection<TestResultMessage> messages,
                TraitCollection traits)
            {
                TestCase = testCase;
                Outcome = outcome;
                AssemblyPath = assemblyPath;
                Type = type;
                Method = method;
                Name = name;
                Time = time;
                ErrorMessage = errorMessage;
                ErrorStackTrace = errorStackTrace;
                Messages = messages;
                Traits = traits;
            }

            public override bool Equals(object obj)
            {
                if (obj is TestResultInfo)
                {
                    TestResultInfo objectToCompare = (TestResultInfo)obj;
                    if (string.Compare(this.ErrorMessage, objectToCompare.ErrorMessage) == 0 && string.Compare(this.ErrorStackTrace, objectToCompare.ErrorStackTrace) == 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

#pragma warning restore 0659

        public void Initialize(TestLoggerEvents events, string testResultsDirPath)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (testResultsDirPath == null)
            {
                throw new ArgumentNullException(nameof(testResultsDirPath));
            }

            var outputPath = Path.Combine(testResultsDirPath, "TestResults.xml");
            InitializeImpl(events, outputPath);
        }

        public void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (parameters.TryGetValue(LogFilePathKey, out string outputPath))
            {
                InitializeImpl(events, outputPath);
            }
            else if (parameters.TryGetValue(DefaultLoggerParameterNames.TestRunDirectory, out string outputDir))
            {
                Initialize(events, outputDir);
            }
            else
            {
                throw new ArgumentException($"Expected {LogFilePathKey} or {DefaultLoggerParameterNames.TestRunDirectory} parameter", nameof(parameters));
            }

            parameters.TryGetValue(EnvironmentKey, out environmentOpt);
        }

        private void InitializeImpl(TestLoggerEvents events, string outputPath)
        {
            events.TestRunMessage += TestMessageHandler;
            events.TestResult += TestResultHandler;
            events.TestRunComplete += TestRunCompleteHandler;

            outputFilePath = Path.GetFullPath(outputPath);

            lock (resultsGuard)
            {
                results = new List<TestResultInfo>();
            }

            localStartTime = DateTime.Now;
        }

        /// <summary>
        /// Called when a test message is received.
        /// </summary>
        internal void TestMessageHandler(object sender, TestRunMessageEventArgs e)
        {
        }

        /// <summary>
        /// Called when a test result is received.
        /// </summary>
        internal void TestResultHandler(object sender, TestResultEventArgs e)
        {
            TestResult result = e.Result;

            var displayName = string.IsNullOrEmpty(result.DisplayName) ? result.TestCase.DisplayName : result.DisplayName;

            if (TryParseName(result.TestCase.FullyQualifiedName, out var typeName, out var methodName, out _))
            {
                lock (resultsGuard)
                {
                    results.Add(new TestResultInfo(
                        result.TestCase,
                        result.Outcome,
                        result.TestCase.Source,
                        typeName,
                        methodName,
                        displayName,
                        result.Duration,
                        result.ErrorMessage,
                        result.ErrorStackTrace,
                        result.Messages,
                        result.TestCase.Traits));
                }
            }
        }

        /// <summary>
        /// Called when a test run is completed.
        /// </summary>
        internal void TestRunCompleteHandler(object sender, TestRunCompleteEventArgs e)
        {
            List<TestResultInfo> resultList;
            lock (resultsGuard)
            {
                resultList = results;
                results = new List<TestResultInfo>();
            }

            var doc = new XDocument(CreateTestSuitesElement(resultList));

            // Create directory if not exist
            var loggerFileDirPath = Path.GetDirectoryName(outputFilePath);
            if (!Directory.Exists(loggerFileDirPath))
            {
                Directory.CreateDirectory(loggerFileDirPath);
            }

            using (var f = File.Create(outputFilePath))
            {
                doc.Save(f);
            }

            String resultsFileMessage = String.Format(CultureInfo.CurrentCulture, "Results File: {0}", outputFilePath);
            Console.WriteLine(resultsFileMessage);
        }

        private XElement CreateTestSuitesElement(List<TestResultInfo> results)
        {
            var allSuites = results.GroupBy(result => result.AssemblyPath)
                .OrderBy(resultsByAssembly => resultsByAssembly.Key)
                .Select((resultsByAssembly, index) => CreateTestSuiteElement(resultsByAssembly, index))
                .ToList();

            var element = new XElement("testsuites", allSuites.Select(suites => suites.Item1));

            var totalTime = TimeSpan.Zero;
            foreach (var suite in allSuites)
                totalTime += suite.Item4;

            element.SetAttributeValue("time", totalTime.TotalSeconds.ToString("N3", CultureInfo.InvariantCulture));
            element.SetAttributeValue("tests", allSuites.Sum(suites => suites.Item2));
            element.SetAttributeValue("failures", allSuites.Sum(suites => suites.Item3));
            return element;
        }

        private Tuple<XElement, int, int, TimeSpan> CreateTestSuiteElement(IGrouping<string, TestResultInfo> resultsByAssembly, int index)
        {
            List<TestResultInfo> testResultAsError = new List<TestResultInfo>();
            var assemblyPath = resultsByAssembly.Key;

            var collections = resultsByAssembly
                .GroupBy(resultsInAssembly => resultsInAssembly.Type)
                .OrderBy(resultsByType => resultsByType.Key)
                .Select(resultsByType => CreateTestCases(resultsByType, testResultAsError))
                .ToList();

            int total = 0;
            int failed = 0;
            var time = TimeSpan.Zero;

            var element = new XElement("testsuite");

            var allCases = new List<XElement>();
            foreach (var collection in collections)
            {
                total += collection.Item2;
                failed += collection.Item3;
                time += collection.Item7;

                allCases.AddRange(collection.Item1);
            }

            element.Add(allCases);

            var assemblyName = assemblyPath.Split('\\').Last();

            element.SetAttributeValue("id", index);
            element.SetAttributeValue("name", assemblyName);

            element.SetAttributeValue("tests", total);
            element.SetAttributeValue("failures", failed);
            element.SetAttributeValue("time", time.TotalSeconds.ToString("N3", CultureInfo.InvariantCulture));

            return Tuple.Create(element, total, failed, time);
        }

        private static Tuple<List<XElement>, int, int, int, int, int, TimeSpan> CreateTestCases(
            IGrouping<string, TestResultInfo> resultsByType,
            List<TestResultInfo> testResultAsError)
        {
            var elements = new List<XElement>();

            int total = 0;
            int passed = 0;
            int failed = 0;
            int skipped = 0;
            int error = 0;
            var time = TimeSpan.Zero;

            foreach (var result in resultsByType)
            {
                switch (result.Outcome)
                {
                    case TestOutcome.Failed:
                        if (IsError(result))
                        {
                            if (!testResultAsError.Contains(result))
                            {
                                error++;
                                testResultAsError.Add(result);
                            }
                            continue;
                        }
                        failed++;
                        break;

                    case TestOutcome.Passed:
                        passed++;
                        break;

                    case TestOutcome.Skipped:
                        skipped++;
                        break;
                }

                total++;
                time += result.Time;

                elements.Add(CreateTestCaseElement(result));
            }

            return Tuple.Create(elements, total, passed, failed, skipped, error, time);
        }

        private static bool IsError(TestResultInfo result)
        {
            string errorMessage = result.ErrorMessage;
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                foreach (var m in JUnitXmlTestLogger.errorTypes)
                {
                    if (errorMessage.IndexOf(m) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static XElement CreateTestCaseElement(TestResultInfo result)
        {
            var element = new XElement("testcase",
                new XAttribute("classname", result.Type),
                new XAttribute("name", result.Name),
                new XAttribute("time", result.Time.TotalSeconds.ToString("N7", CultureInfo.InvariantCulture)));

            if (result.Outcome == TestOutcome.Failed)
            {
                var failure = new XElement("error");
                failure.SetAttributeValue("message", RemoveInvalidXmlChar(result.ErrorMessage));
                failure.Value = RemoveInvalidXmlChar(result.ErrorStackTrace);

                element.Add(failure);
            }

            return element;
        }

        private static bool TryParseName(string testCaseName, out string metadataTypeName, out string metadataMethodName, out string metadataMethodArguments)
        {
            // This is fragile. The FQN is constructed by a test adapter.
            // There is no enforcement that the FQN starts with metadata type name.

            string typeAndMethodName;
            var methodArgumentsStart = testCaseName.IndexOf('(');

            if (methodArgumentsStart == -1)
            {
                typeAndMethodName = testCaseName.Trim();
                metadataMethodArguments = string.Empty;
            }
            else
            {
                typeAndMethodName = testCaseName.Substring(0, methodArgumentsStart).Trim();
                metadataMethodArguments = testCaseName.Substring(methodArgumentsStart).Trim();

                if (metadataMethodArguments[metadataMethodArguments.Length - 1] != ')')
                {
                    metadataTypeName = null;
                    metadataMethodName = null;
                    metadataMethodArguments = null;
                    return false;
                }
            }

            var typeNameLength = typeAndMethodName.LastIndexOf('.');
            var methodNameStart = typeNameLength + 1;

            if (typeNameLength <= 0 || methodNameStart == typeAndMethodName.Length) // No typeName is available
            {
                metadataTypeName = null;
                metadataMethodName = null;
                metadataMethodArguments = null;
                return false;
            }

            metadataTypeName = typeAndMethodName.Substring(0, typeNameLength).Trim();
            metadataMethodName = typeAndMethodName.Substring(methodNameStart).Trim();
            return true;
        }

        private static string RemoveInvalidXmlChar(string str)
        {
            if (str != null)
            {
                // From xml spec (http://www.w3.org/TR/xml/#charsets) valid chars:
                // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]

                // we are handling only #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
                // because C# support unicode character in range \u0000 to \uFFFF
                MatchEvaluator evaluator = new MatchEvaluator(ReplaceInvalidCharacterWithUniCodeEscapeSequence);
                string invalidChar = @"[^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD]";
                return Regex.Replace(str, invalidChar, evaluator);
            }

            return str;
        }

        private static string ReplaceInvalidCharacterWithUniCodeEscapeSequence(Match match)
        {
            char x = match.Value[0];
            return String.Format(@"\u{0:x4}", (ushort)x);
        }
    }
}
