﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MailMerge.Helpers;
using NUnit.Framework;
using TestBase;

namespace MailMerge.OoXml.Tests.FunctionalSpecs
{
    [TestFixture]
    public class WhenMergingADocumentWithSplitMergeFields
    {
        const string TestDocDir = "TestDocuments\\";
        MailMerger sut;
        const string DocWithSplitMergeFieldDocx = "DocWithSplitMergeField.docx";
        const string DocWithoutSplitMergeFieldOrWinstrTextDateRunDocx = "DocWithoutSplitMergeFieldOrWinstrTextDateRun.docx";
        const string DocWithWinstrTextDateRundocx = "DocWithWinstrTextDateRun.docx";
        const string DocWithProblem1docx = "DocWithProblem1.docx";
        //
        static Dictionary<string, string> SplitField = new Dictionary<string, string>
        {
            {"CurrentUser:LastName","CurrentUserLastName"},
        };

        static Dictionary<string, string> NoSplitMergeFieldOrWinstrTextDateRun = new Dictionary<string, string>
        {
            {"Account:Name","AccountName"},
            {"FirstName","FakeFirst"},
            {"LastName","FakeLast"},
            {"CurrentUser:FirstName","CurrentUserFirstName"}
        };

        static Dictionary<string, string> Problem1 = new Dictionary<string, string>
        {
            {"CurrentUser:FirstName","CurrentUserFirstName"}
        };

        static Dictionary<string, string> DateOnly = new Dictionary<string, string>();

        [SetUp]
        public void Setup()
        {
            sut = new MailMerger(Startup.Configure().CreateLogger(GetType()), Startup.Settings);
        }

        [TestCase(DocWithProblem1docx, nameof(Problem1))]
        [TestCase(DocWithWinstrTextDateRundocx, nameof(DateOnly))]
        [TestCase(DocWithSplitMergeFieldDocx, nameof(SplitField))]
        [TestCase(DocWithoutSplitMergeFieldOrWinstrTextDateRunDocx, nameof(NoSplitMergeFieldOrWinstrTextDateRun))]
        public void Merges(string source, string sourceFieldsSource)
        {
            source = TestDocDir + source;
            var sourceFields = GetType()
                                .GetField(sourceFieldsSource,BindingFlags.Static|BindingFlags.NonPublic)
                                .GetValue(this) as Dictionary<string,string>;

            Stream output = null; AggregateException exceptions;

            using (var original = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            try
            {
                (output, exceptions) = sut.Merge(original, sourceFields);

                using (var outFile = new FileInfo(source.Replace(".", " Output.")).OpenWrite())
                {
                    output.Position = 0;
                    output.CopyTo(outFile);
                }

                if (exceptions.InnerExceptions.Any()) { throw exceptions; }

                var outputText = output.AsWordprocessingDocument(false).MainDocumentPart.Document.InnerText;

                sourceFields
                    .Values
                    .ShouldAll(v => outputText.ShouldContain(v));

                sourceFields
                    .Keys
                    .ShouldAll(k => outputText.ShouldNotContain("«" + k + "»"));

                Regex
                    .IsMatch(outputText, @"\<w:instrText [^>]*>MERGEFIELD")
                    .ShouldBeFalse("Merge should have remove all complex field sequences (whether or not they were replaced with text).");

            }
            finally{ output?.Dispose(); }
        }

        [OneTimeSetUp]
        public void EnsureTestDependencies()
        {
            foreach(var testDoc in new[]{DocWithSplitMergeFieldDocx, DocWithoutSplitMergeFieldOrWinstrTextDateRunDocx})
            {
                var expectedTestDoc = TestDocDir + testDoc;
                File.Exists(expectedTestDoc)
                    .ShouldBeTrue(
                        $"Expected to find TestDependency \n\n\"{expectedTestDoc}\"\n\n at "
                        + new FileInfo(expectedTestDoc).FullName + " but didn't. \n"
                        + "Include it in the test project and mark it as as BuildAction=Content, CopyToOutputDirectory=Copy if Newer."
                        );
            }
        }

    }
}
