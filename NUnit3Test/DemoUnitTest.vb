Imports NUnit.Framework

Namespace NUnit3Test

    Public Class DemoUnitTest

        <SetUp>
        Public Sub Setup()
        End Sub

        <Test>
        Sub SuccessfulTest()
            System.Console.WriteLine("Test Console Output=Success")
            Assert.AreEqual(1, 1)
        End Sub

        <Test>
        Sub FailingTest()
            System.Console.WriteLine("Test Console Output=Failure")
            Assert.AreEqual(1, 2)
        End Sub

    End Class

End Namespace