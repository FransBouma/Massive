USE master
GO
CREATE DATABASE [MassiveWriteTests] /* ON PRIMARY (NAME=MassiveWriteTests_dat, FILENAME='c:\mycatalogs\MassiveWriteTests.mdf', SIZE=10MB) */
GO


USE [MassiveWriteTests]
GO
-- ----------------------------------------------------------------------------------------------------------------
-- Schema 'dbo'
-- ----------------------------------------------------------------------------------------------------------------

-- -------[ Tables ]-----------------------------------------------------------------------------------------------

CREATE TABLE [dbo].[Categories] 
(
	[CategoryID] [int] IDENTITY (1,1) NOT NULL, 
	[CategoryName] [nvarchar] (15) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL, 
	[Description] [ntext] NULL, 
	[Picture] [image] NULL 
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[Products] 
(
	[ProductID] [int] IDENTITY (1,1) NOT NULL, 
	[ProductName] [nvarchar] (40) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL, 
	[CategoryID] [int] NULL, 
	[QuantityPerUnit] [nvarchar] (20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL, 
	[UnitPrice] [money] NULL, 
	[UnitsInStock] [smallint] NULL, 
	[UnitsOnOrder] [smallint] NULL, 
	[ReorderLevel] [smallint] NULL, 
	[Discontinued] [bit] NOT NULL 
) ON [PRIMARY]
GO

-- ###############################################################################################################
-- Create statements for Primary key constraints, Foreign key constraints, Unique constraints and Default Values
-- ###############################################################################################################
-- ----------------------------------------------------------------------------------------------------------------
-- Catalog 'MassiveWriteTests'
-- ----------------------------------------------------------------------------------------------------------------

USE [MassiveWriteTests]
GO
-- ----------------------------------------------------------------------------------------------------------------
-- Primary Key constraints for schema 'dbo'
-- ----------------------------------------------------------------------------------------------------------------

ALTER TABLE [dbo].[Categories] WITH NOCHECK 
	ADD CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED 
	( 
		[CategoryID] 
	) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Products] WITH NOCHECK 
	ADD CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED 
	( 
		[ProductID] 
	) ON [PRIMARY]
GO
-- ----------------------------------------------------------------------------------------------------------------
-- Unique constraints for schema 'dbo'
-- ----------------------------------------------------------------------------------------------------------------
-- ----------------------------------------------------------------------------------------------------------------
-- Default values for schema 'dbo'
-- ----------------------------------------------------------------------------------------------------------------
ALTER TABLE [dbo].[Products] 
	ADD CONSTRAINT [DV_Products_UnitPrice] DEFAULT (0) FOR [UnitPrice]
GO
ALTER TABLE [dbo].[Products] 
	ADD CONSTRAINT [DV_Products_UnitsInStock] DEFAULT (0) FOR [UnitsInStock]
GO
ALTER TABLE [dbo].[Products] 
	ADD CONSTRAINT [DV_Products_UnitsOnOrder] DEFAULT (0) FOR [UnitsOnOrder]
GO
ALTER TABLE [dbo].[Products] 
	ADD CONSTRAINT [DV_Products_ReorderLevel] DEFAULT (0) FOR [ReorderLevel]
GO
ALTER TABLE [dbo].[Products] 
	ADD CONSTRAINT [DV_Products_Discontinued] DEFAULT (0) FOR [Discontinued]
GO
-- ----------------------------------------------------------------------------------------------------------------
-- All foreign Key constraints
-- ----------------------------------------------------------------------------------------------------------------

ALTER TABLE [dbo].[Products] 
	ADD CONSTRAINT [FK_Products_Categories] FOREIGN KEY
	(
		[CategoryID] 
	)
	REFERENCES [dbo].[Categories]
	(
		[CategoryID] 
	)
	ON DELETE NO ACTION
	ON UPDATE NO ACTION
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[pr_clearAll]
AS
DELETE FROM Products;
DELETE FROM Categories;

GO
