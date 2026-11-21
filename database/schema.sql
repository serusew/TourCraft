IF DB_ID(N'TourCraft') IS NULL CREATE DATABASE TourCraft;
GO
USE TourCraft;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
CREATE TABLE Destinations (
 Id int IDENTITY PRIMARY KEY, Name nvarchar(100) NOT NULL UNIQUE
);
CREATE TABLE Tours (
 Id int IDENTITY PRIMARY KEY, DestinationId int NOT NULL REFERENCES Destinations(Id),
 Name nvarchar(160) NOT NULL, UNIQUE(DestinationId, Name)
);
CREATE TABLE Departures (
 Id int IDENTITY PRIMARY KEY, TourId int NOT NULL REFERENCES Tours(Id),
 StartDate date NOT NULL, Capacity int NOT NULL CHECK (Capacity > 0),
 Available int NOT NULL,
 IsOpen bit NOT NULL DEFAULT 1,
 CONSTRAINT CK_Departures_Available CHECK (Available >= 0 AND Available <= Capacity)
);
CREATE TABLE Customers (
 Id int IDENTITY PRIMARY KEY, Name nvarchar(120) NOT NULL, Contact nvarchar(160) NOT NULL
);
CREATE TABLE Reservations (
 Id int IDENTITY PRIMARY KEY, DepartureId int NOT NULL REFERENCES Departures(Id),
 CustomerId int NOT NULL REFERENCES Customers(Id), Seats int NOT NULL CHECK (Seats > 0),
 CreatedAt datetime2 NOT NULL DEFAULT SYSDATETIME(), Cancelled bit NOT NULL DEFAULT 0
);
CREATE TABLE Payments (
 Id int IDENTITY PRIMARY KEY, ReservationId int NOT NULL REFERENCES Reservations(Id),
 Amount decimal(12,2) NOT NULL CHECK (Amount > 0), PaidAt datetime2 NOT NULL DEFAULT SYSDATETIME()
);
COMMIT;
GO
