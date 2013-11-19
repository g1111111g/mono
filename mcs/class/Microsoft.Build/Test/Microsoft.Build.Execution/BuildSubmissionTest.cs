//
// BuildSubmissionTest.cs
//
// Author:
//   Atsushi Enomoto (atsushi@xamarin.com)
//
// Copyright (C) 2013 Xamarin Inc. (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NUnit.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace MonoTests.Microsoft.Build.Execution
{
	[TestFixture]
	public class BuildSubmissionTest
	{
		[Test]
		public void ResultBeforeExecute ()
		{
			string empty_project_xml = "<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' />";
			var path = "file://localhost/foo.xml";
			var xml = XmlReader.Create (new StringReader (empty_project_xml), null, path);
			var root = ProjectRootElement.Create (xml);
			var proj = new ProjectInstance (root);
			var bm = new BuildManager ();
			bm.BeginBuild (new BuildParameters ());
			var sub = bm.PendBuildRequest (new BuildRequestData (proj, new string [0]));
			Assert.IsNull (sub.BuildResult, "#1");
		}
		
		// This checks if the build output for each task is written to the loggers and not directly thrown as a Project loader error.
		[Test]
		public void TaskOutputsToLoggers ()
		{
            string project_xml = @"<Project DefaultTargets='Foo;Bar' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Import Project='$(MSBuildToolsPath)\Microsoft.Common.targets' />
  <Target Name='Foo'>
    <ItemGroup>
      <Foo Condition='$(X)' Include='foo.txt' />
    </ItemGroup>
  </Target>
</Project>";
            var xml = XmlReader.Create (new StringReader (project_xml));
            var root = ProjectRootElement.Create (xml);
            var proj = new ProjectInstance (root);
			var sw = new StringWriter ();
			Assert.IsFalse (proj.Build (new ILogger [] {new ConsoleLogger (default (LoggerVerbosity), msg => sw.Write (msg), null, null)}), "#1");
			Assert.IsTrue (sw.ToString ().Contains ("$(X)"), "#2");
		}
		
		[Test]
		public void EndBuildWaitsForSubmissionCompletion ()
		{
			string project_xml = @"<Project DefaultTargets='Wait1Sec' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Target Name='Wait1Sec'>
    <Exec Command='ping 10.1.1.1 -n 1 -w 1000' />
  </Target>
</Project>";
			var xml = XmlReader.Create (new StringReader (project_xml));
			var root = ProjectRootElement.Create (xml);
			var proj = new ProjectInstance (root);
			var bm = new BuildManager ();
			bm.BeginBuild (new BuildParameters ());
			DateTime waitDone = DateTime.MinValue;
			DateTime beforeExec = DateTime.Now;
			var l = new List<BuildSubmission> ();
			for (int i = 0; i < 10; i++) {
				var sub = bm.PendBuildRequest (new BuildRequestData (proj, new string [] { "Wait5Sec" }));
				l.Add (sub);
				sub.ExecuteAsync (delegate { waitDone = DateTime.Now; }, null);
			}
			bm.EndBuild ();
			Assert.IsTrue (l.All (s => s.BuildResult.OverallResult == BuildResultCode.Success), "#1");
			DateTime endBuildDone = DateTime.Now;
			Assert.IsTrue (endBuildDone - beforeExec >= TimeSpan.FromSeconds (1), "#2");
			Assert.IsTrue (endBuildDone > waitDone, "#3");
		}
	}
}
