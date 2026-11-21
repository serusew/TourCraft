using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace TourCraft;

public partial class MainWindow : Window
{
    private readonly AgencyStore store = new();
    public MainWindow()
    {
        InitializeComponent();
        Language=System.Windows.Markup.XmlLanguage.GetLanguage("ru-RU");
        foreach(var grid in new[]{ToursGrid,DeparturesGrid,CustomersGrid,ReservationsGrid,PaymentsGrid,PopularityGrid,OccupancyGrid})
        {
            grid.AutoGeneratingColumn+=(_,e)=>{
                if(e.PropertyName=="Id") e.Column.Header="№";
                if(e.Column is DataGridTextColumn column && column.Binding is System.Windows.Data.Binding binding && e.PropertyType==typeof(DateTime))
                    binding.StringFormat=e.PropertyName=="Отправление"?"dd.MM.yyyy":"dd.MM.yyyy HH:mm";
            };
        }
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (SqlException ex)
        {
            Status.Text = ex.Number switch
            {
                >=50000 and <=50004 => ex.Message.Split('\n')[0],
                547 => "Операция нарушает связь между записями или ограничение данных.",
                2601 or 2627 => "Такая запись уже существует.",
                _ => $"Ошибка SQL Server ({ex.Number}). Проверьте доступ к базе TourCraft и настройку подключения."
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Status.Text = ex is ArgumentException ? "Проверьте формат настройки подключения и введённые значения." : ex.Message;
        }
    }

    private void Refresh(object sender,RoutedEventArgs e) => Run(()=>LoadSection());
    private void Changed(object sender,SelectionChangedEventArgs e)
    {
        if (IsLoaded && e.OriginalSource==Sections) Run(()=>LoadSection());
    }
    private void LoadSection(string? message=null)
    {
        DataGrid grid;
        DataTable table;
        switch(Sections.SelectedIndex)
        {
            case 0: grid=ToursGrid; table=store.Tours(Search.Text.Trim()); break;
            case 1: grid=DeparturesGrid; table=store.Departures(OnlyOpen.IsChecked==true); break;
            case 2: grid=CustomersGrid; table=store.Customers(); break;
            case 3: grid=ReservationsGrid; table=store.Reservations(); break;
            case 4: grid=PaymentsGrid; table=store.Payments(); break;
            default:
                PopularityGrid.ItemsSource=store.Popularity().DefaultView;
                OccupancyGrid.ItemsSource=store.Occupancy().DefaultView;
                Status.Text="Отчёты обновлены."; return;
        }
        grid.ItemsSource=table.DefaultView;
        Status.Text=message ?? (table.Rows.Count==0 ? "Записей не найдено." : $"Записей: {table.Rows.Count}");
    }

    private static DataRowView Selected(DataGrid grid) => grid.SelectedItem as DataRowView
        ?? throw new InvalidOperationException("Выберите строку в таблице.");
    private EntryForm Form(string title)=>new(this,title);
    private bool Confirm(string question)=>MessageBox.Show(this,question,"TourCraft",MessageBoxButton.YesNo,
        MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
    private void NewTour(object sender,RoutedEventArgs e)=>Run(()=>TourForm(null));
    private void EditTour(object sender,RoutedEventArgs e)=>Run(()=>TourForm(Selected(ToursGrid)));
    private void TourForm(DataRowView? row)
    {
        var form=Form(row==null?"Новый маршрут":"Изменение маршрута");
        var dest=form.TextField("Направление",100,row?["Направление"].ToString()??"");
        var name=form.TextField("Маршрут",160,row?["Маршрут"].ToString()??"");
        if(form.ShowDialog()!=true) return;
        store.SaveTour(row==null?null:(int)row["Id"],dest.Text.Trim(),name.Text.Trim());
        LoadSection("Маршрут сохранён.");
    }
    private void DeleteTour(object sender,RoutedEventArgs e)=>Run(()=>{
        var row=Selected(ToursGrid);
        if(!Confirm($"Удалить маршрут «{row["Маршрут"]}»?")) return;
        store.DeleteTour((int)row["Id"]); LoadSection("Маршрут удалён.");
    });
    private void NewCustomer(object sender,RoutedEventArgs e)=>Run(()=>{
        var form=Form("Регистрация клиента"); var name=form.TextField("Имя клиента",120);
        var contact=form.TextField("Телефон или электронная почта",160);
        if(form.ShowDialog()!=true) return;
        store.AddCustomer(name.Text.Trim(),contact.Text.Trim()); LoadSection("Клиент зарегистрирован.");
    });
    private void NewDeparture(object sender,RoutedEventArgs e)=>Run(()=>{
        var form=Form("Новое отправление"); var tours=store.Tours("");
        tours.Columns.Add("Выбор",typeof(string),"Направление + ' · ' + Маршрут");
        var tour=form.Choice("Маршрут",tours,"Выбор"); var date=form.Date(); var capacity=form.Count("Вместимость");
        if(form.ShowDialog()!=true) return;
        store.AddDeparture(tour(),date(),capacity()); LoadSection("Отправление добавлено.");
    });
    private void ChangeStatus(object sender,RoutedEventArgs e)=>Run(()=>{
        var row=Selected(DeparturesGrid); var open=!(bool)row["Набор открыт"];
        if(!Confirm(open?"Открыть набор на отправление?":"Закрыть набор на отправление?")) return;
        store.SetOpen((int)row["Id"],open); LoadSection("Статус набора изменён.");
    });
    private void Book(object sender,RoutedEventArgs e)=>Run(()=>{
        var row=Selected(DeparturesGrid); var form=Form($"Бронирование · {row["Маршрут"]} · {(DateTime)row["Отправление"]:dd.MM.yyyy}");
        var customers=store.Customers();
        customers.Columns.Add("Выбор",typeof(string),"Клиент + ' · ' + Контакт");
        var customer=form.Choice("Клиент",customers,"Выбор"); var seats=form.Count("Количество мест");
        if(form.ShowDialog()!=true) return;
        store.Book((int)row["Id"],customer(),seats()); LoadSection("Бронь оформлена.");
    });
    private void Cancel(object sender,RoutedEventArgs e)=>Run(()=>{
        var row=Selected(ReservationsGrid);
        if(!Confirm($"Отменить бронь №{row["Id"]} и вернуть места на отправление?")) return;
        store.Cancel((int)row["Id"]); LoadSection("Бронь отменена, места возвращены.");
    });
    private void Pay(object sender,RoutedEventArgs e)=>Run(()=>{
        var row=Selected(ReservationsGrid); var form=Form($"Оплата брони №{row["Id"]}"); var amount=form.Amount();
        if(form.ShowDialog()!=true) return;
        store.Pay((int)row["Id"],amount()); LoadSection("Платёж записан в историю оплат.");
    });
}
