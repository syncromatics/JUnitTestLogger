# JUnitTestLogger4GitHub

This is a .NET library that adapts unit test output into the JUnit result format - with error message and stack trace combined into a single value for compatibility with GitHub/GitLab test pipelines

## Purpose of this fork

* The origin JUnitTestLogger creates nice JUnit xml data as required by the specification
* BUT: GitHub respectively the publishing component for unit test results EnricoMi/publish-unit-test-result-action@v1 display either the error message or the stacktrace, but not both
* THAT'S WHY there is the need for a component combining error message and stack trace into a single field

## NuGet package availability

https://www.nuget.org/packages/JUnitTestLogger4GitHub/

## License and Authors

[![license](https://img.shields.io/github/license/CompuMasterGmbH/JUnitTestLogger.svg)](https://github.com/CompuMasterGmbH/JUnitTestLogger/blob/master/LICENSE)
[![GitHub contributors](https://img.shields.io/github/contributors/CompuMasterGmbH/JUnitTestLogger.svg)](https://github.com/CompuMasterGmbH/JUnitTestLogger/graphs/contributors)

This software is made available by GMV Syncromatics Engineering and CompuMaster GmbH under the MIT license.
