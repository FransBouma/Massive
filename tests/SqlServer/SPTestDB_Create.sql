USE master
GO
CREATE DATABASE [MassiveSPTests] /* ON PRIMARY (NAME=MassiveSPTests_dat, FILENAME='c:\mycatalogs\MassiveSPTests.mdf', SIZE=10MB) */
GO

USE [MassiveSPTests]
GO

-- ----------------------------------------------------------------------------------------------------------------
-- Stored procedures
-- ----------------------------------------------------------------------------------------------------------------

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[pr_Plus]
	@FirstArg INT = 0,
	@SecondArg INT = 0
AS
	RETURN @FirstArg + @SecondArg
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[pr_Test]
	@MyInteger INT,
	@OneString VARCHAR(MAX) OUTPUT,
	@AnotherString VARCHAR(MAX) OUTPUT,
	@ThisDate DATETIME OUTPUT
AS
	SET @MyInteger = @MyInteger + 1

	SET @OneString = 'The result is ' + CAST(@MyInteger AS VARCHAR(MAX))
	SET @AnotherString = 'The input string was ''' + ISNULL(@AnotherString, '<null>') + ''''
	SET @ThisDate = GETDATE()

	RETURN @MyInteger
GO
