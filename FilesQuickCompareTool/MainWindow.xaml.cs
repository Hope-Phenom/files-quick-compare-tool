using Microsoft.Win32;
using MultithreadingScaffold;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FilesQuickCompareTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// 以后谁再自己偷偷换了文件然后还乱报BUG，就把他头摁进显示器里
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> FileList = new List<string>();
        private int MuiltThreadGain = 2;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 选择目录按钮事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new FolderBrowserDialog();
            fbd.ShowDialog();

            if (fbd.SelectedPath == null || fbd.SelectedPath == "")
                return;

            if (sender.Equals(Btn_SelectFolder))
                Tbx_FolderPath.Text = fbd.SelectedPath;
            else
                Tbx_FolderPath_2nd.Text = fbd.SelectedPath;
        }

        #region 基础功能模块

        /// <summary>
        /// 获取指定文件的MD5值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        private string GetFileMD5(string filePath)
        {
            try
            {
                FileStream file = new FileStream(filePath, FileMode.Open);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                var retVal = md5.ComputeHash(file);
                file.Close();

                var sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                    sb.Append(retVal[i].ToString("x2"));

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show("GetMD5HashFromFile() fail,error:" + ex.Message);
                return "";
            }
        }

        /// <summary>
        /// 获取指定目录下所有文件的列表,方便后续进行多线程处理
        /// </summary>
        /// <param name="folderPath">目录路径</param>
        /// <returns></returns>
        private void GetFolderFileList(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return;

            var files = Directory.GetFiles(folderPath);
            if (files.Length > 0)
                foreach (var file in files)
                    FileList.Add(file);

            var dirs = Directory.GetDirectories(folderPath);
            if (dirs.Length > 0)
                foreach (var dir in dirs)
                    GetFolderFileList(dir);
        }

        /// <summary>
        /// 使用多线程计算之前处理好的文件列表
        /// </summary>
        /// <param name="basePath">基础路径，列表中的路径会删除此路径部分以获得相对路径</param>
        /// <returns></returns>
        private Dictionary<string, string> CalcFileList(string basePath)
        {
            var dic = new Dictionary<string, string>();

            var mTScaffold = new MTScaffold();
            mTScaffold.Workload = FileList.Count;
            mTScaffold.ThreadLimit = Environment.ProcessorCount * MuiltThreadGain;
            mTScaffold.Worker = (i) =>
            {
                var file = FileList[i];
                var md5 = GetFileMD5(file);

                lock (dic)
                    dic.Add(file.Replace(basePath, ""), md5);
            };
            mTScaffold.Start();

            return dic;
        }

        /// <summary>
        /// 比较两个数据源，并返回一个体现比对结果的DataTable
        /// </summary>
        /// <param name="source_1st">1号源</param>
        /// <param name="source_2nd">2号源</param>
        /// <param name="from_1st">1号源来源</param>
        /// <param name="from_2nd">2号源来源</param>
        /// <returns></returns>
        private DataTable CompareTwoSource(Dictionary<string, string> source_1st, Dictionary<string, string> source_2nd, string from_1st, string from_2nd)
        {
            var dt = new DataTable();
            dt.Columns.Add("结论");
            dt.Columns.Add("文件名");
            dt.Columns.Add("来源");
            dt.Columns.Add("MD5值");

            foreach (var kv in source_1st)
                if (!source_2nd.ContainsKey(kv.Key))
                    dt.Rows.Add("特有", kv.Key, from_1st, kv.Value);
                else if (source_2nd[kv.Key] != source_1st[kv.Key])
                    dt.Rows.Add("不同", kv.Key, "两者", kv.Value);

            foreach (var kv in source_2nd)
                if (!source_1st.ContainsKey(kv.Key))
                    dt.Rows.Add("特有", kv.Key, from_2nd, kv.Value);

            return dt;
        }

        #endregion

        #region UI组件事件

        /// <summary>
        /// 生成比较列表清单事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_MakeCompareList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileList.Clear();
                var currTime = DateTime.Now;

                var sfd = new SaveFileDialog();
                sfd.Filter = "文本文件(.txt)|*.txt";
                sfd.ShowDialog();

                if (sfd.FileName == null || sfd.FileName == "")
                    return;

                GetFolderFileList(Tbx_FolderPath.Text);
                var dict = CalcFileList(Tbx_FolderPath.Text);

                var ls = new List<string>();
                foreach (var keyValue in dict)
                    ls.Add($@"{keyValue.Key},{keyValue.Value}");

                File.WriteAllLines(sfd.FileName, ls.ToArray());

                MessageBox.Show($@"列表生成完毕，共计计算{ls.Count}个文件，耗时{Math.Round((DateTime.Now - currTime).TotalSeconds)}秒！");
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"生成比较清单异常，{ex.Message}");
            }
        }

        /// <summary>
        /// 修改多线程倍率参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                MuiltThreadGain = Convert.ToInt32(Tbx_MTGain.Text);
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// 比价两个目录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_CompareTwoFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Tbx_FolderPath.Text == "" || Tbx_FolderPath_2nd.Text == "")
                    return;

                var currTime = DateTime.Now;

                FileList.Clear();
                GetFolderFileList(Tbx_FolderPath.Text);
                var dict1 = CalcFileList(Tbx_FolderPath.Text);

                FileList.Clear();
                GetFolderFileList(Tbx_FolderPath_2nd.Text);
                var dict2 = CalcFileList(Tbx_FolderPath_2nd.Text);

                var dt = CompareTwoSource(dict1, dict2, Tbx_FolderPath.Text, Tbx_FolderPath_2nd.Text);

                var endTime = DateTime.Now;

                dataGrid_Main.ItemsSource = dt.DefaultView;
                MessageBox.Show($@"目录比对完成，共计比对{dict1.Count + dict2.Count}个文件，耗时{Math.Round((endTime - currTime).TotalSeconds)}秒！");
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"比较目录异常，{ex.Message}");
            }
        }

        /// <summary>
        /// 从清单比较
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_CompareFromList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new OpenFileDialog();
                ofd.Title = "请选择一个清单文件...";
                ofd.Filter = "清单文件(.txt)|*.txt";
                ofd.ShowDialog();

                if (ofd.FileName == null || ofd.FileName == "" || Tbx_FolderPath.Text == "")
                    return;


            }
            catch (Exception ex)
            {
                MessageBox.Show($@"从清单比较异常，{ex.Message}");
            }
        }

        #endregion

    }
}
