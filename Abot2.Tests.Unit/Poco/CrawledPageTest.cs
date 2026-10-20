using Abot2.Core;
using Abot2.Poco;
using AngleSharp.Dom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Abot2.Tests.Unit.Poco
{
    [TestClass]
    public class CrawledPageTest
    {
        [TestMethod]
        public void Constructor_ValidUri_CreatesInstance()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"));
            Assert.AreEqual(null, unitUnderTest.HttpRequestMessage);
            Assert.AreEqual(null, unitUnderTest.HttpResponseMessage);
            Assert.AreEqual(false, unitUnderTest.IsRetry);
            Assert.AreEqual(null, unitUnderTest.ParentUri);
            Assert.IsNotNull(unitUnderTest.Content);
            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.AreEqual("http://a.com/", unitUnderTest.Uri.AbsoluteUri);
            Assert.AreEqual(null, unitUnderTest.HttpRequestException);
            Assert.AreEqual(null, unitUnderTest.ParsedLinks);
        }

        [TestMethod]
        public void Constructor_InvalidUri_ThrowsException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new CrawledPage(null));
        }

        [TestMethod]
        public void AngleSharpHtmlDocument_ContentIsValid_AngleSharpHtmlDocumentIsNotNull()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/")) 
            { 
                Content = new PageContent 
                { 
                    Text = "hi there" 
                } 
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.AreEqual("hi there", unitUnderTest.AngleSharpHtmlDocument.Body.TextContent);
        }

        [TestMethod]
        public void AngleSharpHtmlDocument_RawContentIsNull_AngleSharpHtmlDocumentIsNotNull()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/")) 
            { 
                Content = new PageContent 
                { 
                    Text = null
                }
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.AreEqual("", unitUnderTest.AngleSharpHtmlDocument.Body.TextContent);
        }

        [TestMethod]
        public void AngleSharpHtmlDocument_ToManyNestedTagsInSource1_DoesNotCauseStackOverflowException()
        {
            //FYI this test will not fail, it will just throw an uncatchable stackoverflowexception that will kill the process that runs this test
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = GetFileContent("HtmlAgilityPackStackOverflow1.html")
                }
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.IsTrue(unitUnderTest.AngleSharpHtmlDocument.Body.TextContent.Length > 0);
        }

        [TestMethod]
        public void AngleSharpHtmlDocument_ToManyNestedTagsInSource2_DoesNotCauseStackOverflowException()
        {
            //FYI this test will not fail, it will just throw an uncatchable stackoverflowexception that will kill the process that runs this test
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = GetFileContent("HtmlAgilityPackStackOverflow2.html")
                }
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.IsTrue(unitUnderTest.AngleSharpHtmlDocument.Body.TextContent.Length > 0);
            Assert.IsTrue(GetMaxDepth(unitUnderTest.AngleSharpHtmlDocument) <= CrawledPage.MaxHtmlDocumentDepth);
        }

        [TestMethod]
        public void AngleSharpHtmlDocument_NestedDeeperThanMax_FlattensAndKeepsTextOrderAndLinks()
        {
            //Each nesting level adds a digit on the way in and on the way out so the text order shows whether flattening kept document order
            const int levels = 2000;
            var html = new StringBuilder();
            var expectedText = new StringBuilder();
            for (var i = 0; i < levels; i++)
            {
                html.Append("<div>").Append(i % 10);
                expectedText.Append(i % 10);
            }
            html.Append("<a href=\"http://a.com/deep\">link</a>");
            expectedText.Append("link");
            for (var i = levels - 1; i >= 0; i--)
            {
                html.Append(i % 10).Append("</div>");
                expectedText.Append(i % 10);
            }

            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = html.ToString()
                }
            };

            Assert.AreEqual(expectedText.ToString(), unitUnderTest.AngleSharpHtmlDocument.Body.TextContent);
            Assert.AreEqual(levels, unitUnderTest.AngleSharpHtmlDocument.QuerySelectorAll("div").Length);
            Assert.AreEqual("http://a.com/deep", unitUnderTest.AngleSharpHtmlDocument.QuerySelector("a").GetAttribute("href"));
            Assert.IsTrue(GetMaxDepth(unitUnderTest.AngleSharpHtmlDocument) <= CrawledPage.MaxHtmlDocumentDepth);
            Assert.IsTrue(unitUnderTest.AngleSharpHtmlDocument.DocumentElement.OuterHtml.Contains("<a href=\"http://a.com/deep\">link</a>"));
        }

        [TestMethod]
        public void AngleSharpHtmlDocument_NestedShallowerThanMax_IsNotFlattened()
        {
            const int levels = 100;
            var html = new StringBuilder();
            for (var i = 0; i < levels; i++)
                html.Append("<div>");
            html.Append("text");
            for (var i = 0; i < levels; i++)
                html.Append("</div>");

            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = html.ToString()
                }
            };

            //document > html > body > 100 divs > text
            Assert.AreEqual(levels + 3, GetMaxDepth(unitUnderTest.AngleSharpHtmlDocument));
        }


        [TestMethod]
        public void AngleSharpDocument_RawContentIsNull_AngleSharpDocumentIsNotNull()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = null
                }
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
        }

        [TestMethod]
        public void AngleSharpDocument_ToManyNestedTagsInSource1_DoesNotCauseStackOverflowException()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = GetFileContent("HtmlAgilityPackStackOverflow1.html")
                }
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.IsTrue(unitUnderTest.AngleSharpHtmlDocument.DocumentElement.OuterHtml.Length > 1);
        }

        [TestMethod]
        public void AngleSharpDocument_ToManyNestedTagsInSource2_DoesNotCauseStackOverflowException()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = GetFileContent("HtmlAgilityPackStackOverflow2.html")
                }
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.IsTrue(unitUnderTest.AngleSharpHtmlDocument.DocumentElement.OuterHtml.Length > 1);
        }

        [TestMethod]
        public void AngleSharp_EncodingChangedTwice_IsLoaded()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                Content = new PageContent
                {
                    Text = @"<div>hehe</div><meta http-equiv=""Content-Type"" content=""text/html; charset=iso-8859-1""><meta http-equiv=""content-type"" content=""text/html; charset=utf-8"" /><div>hi</div>"
                }
            };

            Assert.IsNotNull(unitUnderTest.AngleSharpHtmlDocument);
            Assert.AreEqual(7, unitUnderTest.AngleSharpHtmlDocument.All.Length);
        }


        [TestMethod]
        public void ToString_HttpResponseDoesNotExists_MessageHasUri()
        {
            Assert.AreEqual("http://localhost.fiddler:1111/", new CrawledPage(new Uri("http://localhost.fiddler:1111/")).ToString());
        }

        [TestMethod]
        public void ToString_HttpResponseExists_MessageHasUriAndStatus()
        {
            var unitUnderTest = new CrawledPage(new Uri("http://a.com/"))
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            };

            Assert.AreEqual("http://a.com/[200]", unitUnderTest.ToString());
        }

        [TestMethod]
        public void Elapsed_ReturnsDiffInMilli()
        {
            var uut = new CrawledPage(new Uri("http://a.com"))
            {
                RequestStarted = DateTime.Now.AddSeconds(-5),
                RequestCompleted = DateTime.Now
            };

            Assert.IsTrue(uut.Elapsed >= 5000, "Expected >= 5000 but was " + uut.Elapsed);
        }

        private string GetFileContent(string fileName)
        {
            //var testFile = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, fileName));
            var testFile = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName));
            
            if (!File.Exists(testFile))
                throw new ApplicationException("Cannot find file " + fileName);

            return File.ReadAllText(testFile);
        }

        private int GetMaxDepth(INode root)
        {
            var max = 0;
            var pending = new Stack<(INode Node, int Depth)>();
            pending.Push((root, 0));
            while (pending.Count > 0)
            {
                var (node, depth) = pending.Pop();
                max = Math.Max(max, depth);
                foreach (var child in node.ChildNodes)
                    pending.Push((child, depth + 1));
            }

            return max;
        }
    }
}
