Imports System
Imports Xunit

Namespace xUnitTest

    Public Class DemoUnitTest

        <Fact>
        Sub SuccessfulTest()
            System.Console.WriteLine("Test Console Output=Success")
            Assert.Equal(1, 1)
        End Sub

        <Fact>
        Sub FailingTest()
            System.Console.WriteLine("Test Console Output=Failure")
            Assert.Equal(1, 2)
        End Sub

    End Class

End Namespace