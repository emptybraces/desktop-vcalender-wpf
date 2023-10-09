using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;
namespace desktop_vcalender_wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<Data> _dataList = new List<Data>();
        DateTime _startDate;
        SaveData _save;

        public MainWindow()
        {
            InitializeComponent();
            _save = SaveData.Load();

            var bounds = Rect.Parse(Properties.Settings.Default.Bounds);
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;

            const int start_num = -60;
            _startDate = DateTime.Today.AddDays(start_num);
            for (int i = 0; i < 120; i++)
            {
                var date = _startDate.AddDays(i);
                _dataList.Add(new Data { Date = date, Memo = _save.GetMemo(date), TabIndex = i });
                if (start_num + i == 0)
                    c_listview.SelectedIndex = i;
                else if (start_num + i == 15)
                    c_listview.ScrollIntoView(_dataList[i]);
            }
            c_listview.ItemsSource = _dataList;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Bounds = RestoreBounds.ToString();
            Properties.Settings.Default.Save();
            SaveData.Save(_save);
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        Point mousePoint;
        private void c_listview_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var mp = e.GetPosition(this);
                var diff = new Point(mp.X - mousePoint.X, mp.Y - mousePoint.Y);
                Left += diff.X;
                Top += diff.Y;
            }
            else
            {
                mousePoint = e.GetPosition(this);
            }
        }


        private void TextBox_LostKeyboardFocus(object sender, RoutedEventArgs e)
        {
            if (GetSelectedIndex(sender, out var idx))
            {
                var memo = ((TextBox)sender).Text;
                _save.SetMemo(_dataList[idx].Date, memo);
                Debug.WriteLine(_dataList[idx].Date + memo);
            }

        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Keyboard.ClearFocus();
            else if (e.Key == Key.Escape)
                Close();
            else if (e.Key == Key.Tab && GetSelectedIndex(sender, out var idx) && ++idx < _dataList.Count)
            {
                // 次のテキストボックスにフォーカスするには？
                Keyboard.ClearFocus();
            }
        }

        bool GetSelectedIndex(object sender, out int idx)
        {
            if (sender is Control ctl && ctl.DataContext is Data data)
            {
                if (c_listview.ItemsSource is IList<Data> list)
                {
                    idx = list.IndexOf(data);
                    return true;
                }
            }
            idx = -1;
            return false;
        }
    }

    public class Data
    {
        public DateTime Date { get; set; }
        public string DateString => Date.ToString("MM/dd(ddd)");
        public string Memo { get; set; }
        public int TabIndex { get; set; }
    }

    public class SaveData
    {
        static readonly string path = "save.txt";
        public List<Item> Items = new List<Item>();
        string KeyFromDate(DateTime date) => date.ToString("yyyyMMdd");
        public void SetMemo(DateTime date, string value)
        {
            var key = KeyFromDate(date);
            var index = Items.FindIndex(e => e.Key == key);
            if (-1 == index)
                Items.Add(new Item { Key = key, Value = value });
            else
                Items[index].Value = value;
        }
        public string GetMemo(DateTime date)
        {
            var key = KeyFromDate(date);
            var index = Items.FindIndex(e => e.Key == key);
            return -1 == index ? "" : Items[index].Value;
        }
        public class Item
        {
            public string Key;
            public string Value;
        }
        public static string Save(SaveData control)
        {
            control.Items = control.Items.Where(e => !string.IsNullOrWhiteSpace(e.Value)).ToList();
            var writer = new StringWriter(); // 出力先のWriterを定義
            var serializer = new XmlSerializer(typeof(SaveData));
            serializer.Serialize(writer, control);
            var xml = writer.ToString();
            File.WriteAllText(path, xml);
            Console.WriteLine(xml);
            return xml;
        }

        public static SaveData Load()
        {
            if (!File.Exists(path))
                return new SaveData();
            var xml = File.ReadAllText(path);
            var serializer = new XmlSerializer(typeof(SaveData));
            var deserializedBook = serializer.Deserialize(new StringReader(xml)) as SaveData;
            return deserializedBook;
        }
    }
}
