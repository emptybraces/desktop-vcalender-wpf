using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
namespace desktop_vcalender_wpf
{
    public enum State { None, Plan, Must, Pending, Done }

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

            var bounds = Properties.Settings.Default.Bounds.Split(",");
            if (bounds.Length == 4)
            {
                if (int.TryParse(bounds[0], out int left)) Left = left;
                if (int.TryParse(bounds[1], out int top)) Top = top;
                if (int.TryParse(bounds[2], out int width)) Width = width;
                if (int.TryParse(bounds[3], out int height)) Height = height;
            }

            const int start_num = -60;
            int focus_idx = -1;
            _startDate = DateTime.Today.AddDays(start_num);
            for (int i = 0; i < 120; i++)
            {
                var date = _startDate.AddDays(i);
                var savedata = _save.Get(date);
                var data = new Data();
                data.Date = date;
                data.Memo = savedata?.Memo ?? "";
                data.State.Value = savedata?.State ?? State.None;
                data.TabIndex = 1;
                _dataList.Add(data);
                if ((data.State.Value == State.Plan || data.State.Value == State.Must) && date < DateTime.Today)
                    data.State.Value = State.Pending;
                if (focus_idx == -1)
                {
                    if (data.State.Value == State.Pending || i + start_num == 0)
                        focus_idx = i;
                }
            }
            if (focus_idx == -1)
                focus_idx = -start_num;
            c_listview.ItemsSource = _dataList;
            c_listview.ScrollIntoView(_dataList[^1]);
            c_listview.ScrollIntoView(_dataList[focus_idx]);
            Debug.WriteLine(focus_idx);
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
                Decorator border = VisualTreeHelper.GetChild(c_listview, 0) as Decorator;
                if (border != null)
                {
                    // Get scrollviewer
                    ScrollViewer scrollViewer = border.Child as ScrollViewer;
                    if (scrollViewer != null)
                        scrollViewer.ScrollToHorizontalOffset(0);
                }
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

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (GetSelectedIndex(sender, out var idx))
            {
                MenuItem menuItem = (MenuItem)e.Source;
                ContextMenu menu = (ContextMenu)menuItem.Parent;
                //var item = (DockPanel)menu.PlacementTarget;

                _dataList[idx].State.Value = (State)Enum.Parse(typeof(State), menuItem.Header.ToString());
                //_save.SetState(_dataList[idx].Date, _dataList[idx].State);
            }
        }
    }

    public class Data
    {
        public DateTime Date { get; set; }
        public string DateString => Date.ToString("MM/dd(ddd)");
        public string Memo { get; set; } = "";
        public int TabIndex { get; set; }
        public ReactiveProperty<State> State { get; } = new();
    }

    public class SaveData
    {
        static readonly string path = "save.txt";
        public List<Item> Items = new List<Item>();
        string KeyFromDate(DateTime date) => date.ToString("yyyyMMdd");
        public void SetMemo(DateTime date, string memo)
        {
            var key = KeyFromDate(date);
            var index = Items.FindIndex(e => e.Date == key);
            if (-1 == index)
                Items.Add(new Item { Date = key, Memo = memo });
            else
                Items[index].Memo = memo;
        }
        public void SetState(DateTime date, State state)
        {
            var key = KeyFromDate(date);
            var index = Items.FindIndex(e => e.Date == key);
            if (-1 == index)
                Items.Add(new Item { Date = key, State = state });
            else
                Items[index].State = state;
        }
        public Item Get(DateTime date)
        {
            var key = KeyFromDate(date);
            var index = Items.FindIndex(e => e.Date == key);
            return -1 == index ? null : Items[index];
        }
        public class Item
        {
            public string Date;
            public string Memo;
            public State State;
        }
        public static string Save(SaveData save)
        {
            save.Items = save.Items.Where(e => !string.IsNullOrWhiteSpace(e.Memo) || e.State != State.None).ToList();
            var writer = new StringWriter(); // 出力先のWriterを定義
            var serializer = new XmlSerializer(typeof(SaveData));
            serializer.Serialize(writer, save);
            var xml = writer.ToString();
            File.WriteAllText(path, xml);
            Debug.WriteLine(xml);
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
