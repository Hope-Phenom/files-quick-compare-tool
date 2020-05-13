using Microsoft.Win32;
using MultithreadingScaffold;
using System;
using System.Collections.Generic;
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

        private void CompareFunction()
        {

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

        #endregion

    }
}
