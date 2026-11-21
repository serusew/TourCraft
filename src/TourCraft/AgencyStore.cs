using System.Data;
using Microsoft.Data.SqlClient;

namespace TourCraft;

internal sealed class AgencyStore
{
    private readonly string connectionString = Environment.GetEnvironmentVariable("TOURCRAFT_CONNECTION")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=TourCraft;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=4";

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static SqlParameter Param(string name, SqlDbType type, object value, int size = 0) =>
        new(name, type, size) { Value = value };

    private DataTable Query(string sql, params SqlParameter[] parameters)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        using var reader = command.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private void Execute(string sql, params SqlParameter[] parameters)
    {
        using var connection = Open();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        command.ExecuteNonQuery();
    }

    public DataTable Tours(string search) => Query("""
        SELECT t.Id, d.Name AS Направление,t.Name AS Маршрут FROM Tours t
        JOIN Destinations d ON d.Id=t.DestinationId
        WHERE @search=N'' OR CHARINDEX(@search,t.Name)>0 OR CHARINDEX(@search,d.Name)>0 ORDER BY d.Name,t.Name
        """, Param("@search", SqlDbType.NVarChar, search, 160));

    public DataTable Departures(bool openOnly) => Query("""
        SELECT dep.Id,t.Name AS Маршрут,dep.StartDate AS Отправление,dep.Capacity AS Вместимость,
               dep.Available AS Свободно,dep.IsOpen AS [Набор открыт] FROM Departures dep
        JOIN Tours t ON t.Id=dep.TourId WHERE @filter=0 OR dep.IsOpen=1 ORDER BY dep.StartDate,dep.Id
        """, Param("@filter", SqlDbType.Bit, openOnly));

    public DataTable Customers() => Query("SELECT Id,Name AS Клиент,Contact AS Контакт FROM Customers ORDER BY Name,Id");
    public DataTable Reservations() => Query("""
        SELECT r.Id,c.Name AS Клиент,t.Name AS Маршрут,dep.StartDate AS Отправление,r.Seats AS Мест,
               r.CreatedAt AS Оформлена,r.Cancelled AS Отменена FROM Reservations r
        JOIN Customers c ON c.Id=r.CustomerId JOIN Departures dep ON dep.Id=r.DepartureId
        JOIN Tours t ON t.Id=dep.TourId ORDER BY r.Id DESC
        """);
    public DataTable Payments() => Query("""
        SELECT p.Id,r.Id AS Бронь,c.Name AS Клиент,t.Name AS Маршрут,p.Amount AS [Сумма, ₽],p.PaidAt AS Дата
        FROM Payments p JOIN Reservations r ON r.Id=p.ReservationId JOIN Customers c ON c.Id=r.CustomerId
        JOIN Departures dep ON dep.Id=r.DepartureId JOIN Tours t ON t.Id=dep.TourId ORDER BY p.Id DESC
        """);
    public DataTable Popularity() => Query("""
        SELECT d.Name AS Направление,COALESCE(SUM(CAST(r.Seats AS bigint)),0) AS [Забронировано мест]
        FROM Destinations d LEFT JOIN Tours t ON t.DestinationId=d.Id
        LEFT JOIN Departures dep ON dep.TourId=t.Id LEFT JOIN Reservations r ON r.DepartureId=dep.Id AND r.Cancelled=0
        GROUP BY d.Id,d.Name ORDER BY 2 DESC,d.Name
        """);
    public DataTable Occupancy() => Query("""
        SELECT dep.Id,t.Name AS Маршрут,dep.StartDate AS Отправление,dep.Capacity-dep.Available AS Занято,
        CAST(100.0*(dep.Capacity-dep.Available)/dep.Capacity AS decimal(5,1)) AS [Загрузка, %]
        FROM Departures dep JOIN Tours t ON t.Id=dep.TourId ORDER BY dep.StartDate,dep.Id
        """);

    public void SaveTour(int? id,string destination,string name) => Execute("""
        SET XACT_ABORT ON;
        BEGIN TRAN;
        DECLARE @d int=(SELECT Id FROM Destinations WITH (UPDLOCK,HOLDLOCK) WHERE Name=@destination);
        IF @d IS NULL BEGIN INSERT Destinations(Name) VALUES(@destination); SET @d=SCOPE_IDENTITY(); END;
        IF @id IS NULL INSERT Tours(DestinationId,Name) VALUES(@d,@name);
        ELSE BEGIN
          UPDATE Tours SET DestinationId=@d,Name=@name WHERE Id=@id;
          IF @@ROWCOUNT=0 THROW 50001,N'Маршрут уже удалён.',1;
        END;
        COMMIT;
        """,Param("@id",SqlDbType.Int,(object?)id??DBNull.Value),Param("@destination",SqlDbType.NVarChar,destination,100),Param("@name",SqlDbType.NVarChar,name,160));
    public void DeleteTour(int id) => Execute("DELETE FROM Tours WHERE Id=@id",Param("@id",SqlDbType.Int,id));
    public void AddCustomer(string name,string contact) => Execute("INSERT Customers(Name,Contact) VALUES(@name,@contact)",
        Param("@name",SqlDbType.NVarChar,name,120),Param("@contact",SqlDbType.NVarChar,contact,160));
    public void AddDeparture(int tour,DateTime date,int capacity) => Execute("""
        INSERT Departures(TourId,StartDate,Capacity,Available) VALUES(@tour,@date,@capacity,@capacity)
        """,Param("@tour",SqlDbType.Int,tour),Param("@date",SqlDbType.Date,date),Param("@capacity",SqlDbType.Int,capacity));
    public void SetOpen(int id,bool open) => Execute("UPDATE Departures SET IsOpen=@open WHERE Id=@id",
        Param("@id",SqlDbType.Int,id),Param("@open",SqlDbType.Bit,open));
    public void Book(int departure,int customer,int seats) => Execute("""
        SET XACT_ABORT ON;
        BEGIN TRAN;
        UPDATE Departures SET Available=Available-@seats WHERE Id=@dep AND IsOpen=1 AND @seats>0 AND Available>=@seats;
        IF @@ROWCOUNT=0 THROW 50002,N'Бронь отклонена. Проверьте остаток мест и статус набора.',1;
        INSERT Reservations(DepartureId,CustomerId,Seats) VALUES(@dep,@customer,@seats);
        COMMIT;
        """,Param("@dep",SqlDbType.Int,departure),Param("@customer",SqlDbType.Int,customer),Param("@seats",SqlDbType.Int,seats));
    public void Cancel(int id) => Execute("""
        SET XACT_ABORT ON;
        BEGIN TRAN;
        UPDATE Reservations SET Cancelled=1 WHERE Id=@id AND Cancelled=0;
        IF @@ROWCOUNT=0 THROW 50003,N'Действующая бронь не найдена.',1;
        UPDATE dep SET Available=dep.Available+r.Seats FROM Departures dep
        JOIN Reservations r ON r.DepartureId=dep.Id WHERE r.Id=@id;
        COMMIT;
        """,Param("@id",SqlDbType.Int,id));
    public void Pay(int id,decimal amount) => Execute("""
        SET XACT_ABORT ON;
        BEGIN TRAN;
        INSERT Payments(ReservationId,Amount)
        SELECT Id,@amount FROM Reservations WITH (UPDLOCK) WHERE Id=@id AND Cancelled=0;
        IF @@ROWCOUNT=0 THROW 50004,N'Оплата возможна только для действующей брони.',1;
        COMMIT;
        """,Param("@id",SqlDbType.Int,id),new SqlParameter("@amount",SqlDbType.Decimal){Precision=12,Scale=2,Value=amount});
}
