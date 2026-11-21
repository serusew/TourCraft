using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TourCraft;
internal sealed class EntryForm : Window
{
    private readonly StackPanel fields = new();
    private readonly List<Func<string?>> checks = new();

    public EntryForm(Window owner,string title)
    {
        Owner=owner; Title=title; Width=440; SizeToContent=SizeToContent.Height;
        ResizeMode=ResizeMode.NoResize; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel();
        Content=new Border { Padding=new Thickness(22), Child=panel }; panel.Children.Add(fields);
        panel.Children.Insert(0,new TextBlock { Text=title, FontSize=17, TextWrapping=TextWrapping.Wrap });
        var buttons=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,16,0,0) };
        panel.Children.Add(buttons);
        var save=new Button { Content="Сохранить", IsDefault=true };
        save.Click+=(_,_)=>{
            foreach(var check in checks)
            {
                var error=check();
                if(error!=null) { MessageBox.Show(this,error,Title); return; }
            }
            DialogResult=true;
        };
        buttons.Children.Add(save); buttons.Children.Add(new Button { Content="Назад",IsCancel=true });
    }
    private void Add(string label,Control control)
    {
        fields.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,10,0,4)});
        fields.Children.Add(control);
    }
    public TextBox TextField(string label,int limit,string initial="")
    {
        var field=new TextBox {Text=initial,MaxLength=limit};
        Add(label,field); checks.Add(()=>string.IsNullOrWhiteSpace(field.Text)? $"Заполните поле «{label}».":null);
        return field;
    }
    public Func<int> Count(string label)
    {
        var field=TextField(label,9,"1");
        checks.Add(()=>int.TryParse(field.Text,out var n)&&n>0?null:"Введите целое положительное число.");
        return ()=>int.Parse(field.Text);
    }
    public Func<decimal> Amount()
    {
        var field=TextField("Сумма, ₽",16);
        bool Valid()=>decimal.TryParse(field.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out var n)
            && n>0 && n<=9999999999.99m && decimal.Round(n,2)==n;
        checks.Add(()=>Valid()?null:"Введите положительную сумму с точностью до копеек.");
        return ()=>decimal.Parse(field.Text);
    }
    public Func<int> Choice(string label,DataTable table,string display)
    {
        if(table.Rows.Count==0) throw new InvalidOperationException($"Сначала заполните список «{label}».");
        var field=new ComboBox { ItemsSource=table.DefaultView,DisplayMemberPath=display,SelectedValuePath="Id",SelectedIndex=0,Padding=new Thickness(6) };
        Add(label,field); return ()=>Convert.ToInt32(field.SelectedValue);
    }
    public Func<DateTime> Date()
    {
        var field=new DatePicker {SelectedDate=DateTime.Today};
        Add("Дата отправления",field); checks.Add(()=>field.SelectedDate.HasValue?null:"Выберите дату отправления.");
        return ()=>field.SelectedDate!.Value;
    }
}
