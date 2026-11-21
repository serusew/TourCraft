USE TourCraft;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM Destinations) THROW 50000, N'Наполнение предназначено для пустой базы.', 1;
INSERT Destinations(Name) VALUES (N'Байкал'), (N'Алтай');
INSERT Tours(DestinationId, Name) SELECT Id, CASE Name WHEN N'Байкал' THEN N'Остров Ольхон' ELSE N'Чуйский тракт' END FROM Destinations;
INSERT Departures(TourId,StartDate,Capacity,Available,IsOpen)
SELECT Id, DATEADD(day,14,GETDATE()),12,12,1 FROM Tours;
INSERT Departures(TourId,StartDate,Capacity,Available,IsOpen)
SELECT Id, DATEADD(day,30,GETDATE()),8,8,0 FROM Tours WHERE Name=N'Остров Ольхон';
INSERT Customers(Name, Contact) VALUES (N'Анна Примерова', N'anna@example.org');
DECLARE @departure int=(SELECT MIN(Id) FROM Departures WHERE IsOpen=1);
INSERT Reservations(DepartureId, CustomerId, Seats) SELECT @departure,Id,2 FROM Customers;
DECLARE @reservation int=CONVERT(int,SCOPE_IDENTITY());
UPDATE Departures SET Available=Available-2 WHERE Id=@departure;
INSERT Payments(ReservationId,Amount) VALUES (@reservation,5000);
COMMIT;
GO
