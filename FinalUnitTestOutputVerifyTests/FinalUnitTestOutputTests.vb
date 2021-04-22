Imports NUnit.Framework

Namespace FinalUnitTestOutputVerifyTests

    Public Class Tests

        <SetUp>
        Public Sub Setup()
        End Sub

        <Test>
        Public Sub PreviousTestResultsDirectoryExists()
            Assert.IsTrue(System.IO.File.Exists("previous-test-results/README.txt"))
        End Sub

        <Test>
        Public Sub PreviousTestResultsXUnitOutputExists()
            Assert.IsTrue(System.IO.File.Exists("previous-test-results/testresults.xunit.xml"), "File missing")
            Dim FInfo As New System.IO.FileInfo("previous-test-results/testresults.xunit.xml")
            Assert.AreNotEqual(0&, FInfo.Length, "Size > 0 required")
            Assert.LessOrEqual(Now.AddDays(-1), FInfo.LastWriteTime, "Test results file must be written within last 24 hours")
        End Sub

        <Test>
        Public Sub PreviousTestResultsNUnit3OutputExists()
            Assert.IsTrue(System.IO.File.Exists("previous-test-results/testresults.nunit3.xml"), "File missing")
            Dim FInfo As New System.IO.FileInfo("previous-test-results/testresults.nunit3.xml")
            Assert.AreNotEqual(0&, FInfo.Length, "Size > 0 required")
            Assert.LessOrEqual(Now.AddDays(-1), FInfo.LastWriteTime, "Test results file must be written within last 24 hours")
        End Sub

        <Test>
        Public Sub XUnitTestResultsOutput()
            Dim XmlContent As String = System.IO.File.ReadAllText("previous-test-results/testresults.xunit.xml")
            VerifyUnitTestResultsOutput(XmlContent, UnitTestStyle.xUnit)
        End Sub

        <Test>
        Public Sub NUnit3TestResultsOutput()
            Dim XmlContent As String = System.IO.File.ReadAllText("previous-test-results/testresults.nunit3.xml")
            VerifyUnitTestResultsOutput(XmlContent, UnitTestStyle.NUnit3)
        End Sub

        Private Enum UnitTestStyle As Byte
            xUnit = 1
            NUnit3 = 2
        End Enum

        Private Sub VerifyUnitTestResultsOutput(xmlContent As String, unitTestStyle As UnitTestStyle)
            'XML must contain test result for tests "SuccessfulTest" and "FailingTest"
            Assert.IsTrue(xmlContent.Contains("SuccessfulTest"))
            Assert.IsTrue(xmlContent.Contains("FailingTest"))
            'XML must contain error details
            Assert.IsTrue(xmlContent.Contains("<error>"))
            Assert.IsTrue(xmlContent.Contains("</error>"))
            'XML must contain error details together with stack trace in tag value, attribute "ErrorMessage" is prohibited
            Assert.IsFalse(xmlContent.Contains(" ErrorMessage="))
            'XML contains expected test failure details and expected stack trace information
            Assert.IsTrue(xmlContent.Contains("Expected: 1"))
            Select Case unitTestStyle
                Case UnitTestStyle.xUnit
                    Assert.IsTrue(xmlContent.Contains("Actual:   2"))
                    Assert.IsTrue(xmlContent.Contains("at xUnitTest.xUnitTest.DemoUnitTest.FailingTest() in"))
                Case UnitTestStyle.NUnit3
                    Assert.IsTrue(xmlContent.Contains("was:  2"))
                    Assert.IsTrue(xmlContent.Contains("at NUnit3Test.NUnit3Test.DemoUnitTest.FailingTest() in"))
                Case Else
                    Throw New NotImplementedException
            End Select
            Assert.IsTrue(xmlContent.Contains(":line "))
            ''XML must contain console output data from tests
            'Assert.IsTrue(xmlContent.Contains("Test Console Output=Failure"))
            'Assert.IsTrue(xmlContent.Contains("Test Console Output=Success"))
        End Sub

    End Class

End Namespace