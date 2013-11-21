﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using iSpyApplication.Controls;
using iSpyApplication.Properties;

namespace iSpyApplication
{
    partial class MainForm
    {
        internal void SelectMediaRange(PreviewBox controlFrom, PreviewBox controlTo)
        {
            lock (flowPreview.Controls)
            {
                if (controlFrom != null && controlTo != null)
                {
                    if (flowPreview.Controls.Contains(controlFrom) && flowPreview.Controls.Contains(controlTo))
                    {
                        bool start = false;
                        foreach (Control c in flowPreview.Controls)
                        {
                            var p = c as PreviewBox;
                            if (p != null)
                            {
                                if (p == controlFrom)
                                {
                                    start = true;
                                }
                                if (start)
                                    p.Selected = true;
                                if (p == controlTo)
                                    break;
                            }
                        }
                        start = false;
                        foreach (Control c in flowPreview.Controls)
                        {
                            var p = c as PreviewBox;
                            if (p != null)
                            {
                                if (p == controlTo)
                                {
                                    start = true;
                                }
                                if (start)
                                    p.Selected = true;
                                if (p == controlFrom)
                                    break;
                            }
                        }
                    }
                }
                flowPreview.Invalidate(true);
            }
        }

        public void DeleteSelectedMedia()
        {
            flowPreview.SuspendLayout();
            lock (flowPreview.Controls)
            {
                for (int i = 0; i < flowPreview.Controls.Count; i++)
                {
                    var pb = flowPreview.Controls[i] as PreviewBox;
                    if (pb!=null && pb.Selected)
                    {
                        RemovePreviewBox(pb);
                        i--;
                    }
                }
            }
            flowPreview.ResumeLayout(true);
        }

        private void RemovePreviewBox(PreviewBox pb)
        {
            string[] parts = pb.FileName.Split('\\');
            string fn = parts[parts.Length - 1];
            string id = fn.Substring(0, fn.IndexOf('_'));

            try
            {
                //movie
                FileOperations.Delete(pb.FileName);
                var cw = GetCameraWindow(Convert.ToInt32(id));
                if (cw!=null)
                {
                    cw.RemoveFile(fn);
                }
                

                //preview
                string dir = pb.FileName.Substring(0, pb.FileName.LastIndexOf("\\", StringComparison.Ordinal));

                var lthumb = dir + @"\thumbs\" + fn.Substring(0, fn.LastIndexOf(".", StringComparison.Ordinal)) + "_large.jpg";
                FileOperations.Delete(lthumb);

                lthumb = dir + @"\thumbs\" + fn.Substring(0, fn.LastIndexOf(".", StringComparison.Ordinal)) + ".jpg";
                FileOperations.Delete(lthumb);
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            flowPreview.Controls.Remove(pb);
            pb.MouseDown -= PbMouseDown;
            pb.MouseEnter -= PbMouseEnter;
            pb.Dispose();

            NeedsMediaRefresh = DateTime.Now;
           
        }

        public void LoadPreviews()
        {
            if (!flowPreview.Loading)
            {
                NeedsMediaRefresh = DateTime.MinValue;
                UISync.Execute(RenderPreviewBoxes);
                
            }
        }

        private void RenderPreviewBoxes()  {

            lock (flowPreview.Controls)
            {
                if (MediaPanelPage * Conf.PreviewItems > MasterFileList.Count-1)
                {
                    MediaPanelPage = 0;
                }

                if (Filter.Filtered)
                {
                    var l = MasterFileList.Where(
                            p =>
                            ((p.ObjectTypeId == 2 && Filter.CheckedCameraIDs.Contains(p.ObjectId)) ||
                             (p.ObjectTypeId == 1 && Filter.CheckedMicIDs.Contains(p.ObjectId))) &&
                            p.CreatedDateTicks > Filter.StartDate.Ticks && p.CreatedDateTicks < Filter.EndDate.Ticks).ToList
                            ();
                    int pageCount = (l.Count - 1) / Conf.PreviewItems + 1;

                    var displayList = l.OrderByDescending(p => p.CreatedDateTicks).Skip(MediaPanelPage * Conf.PreviewItems).Take(Conf.PreviewItems).ToList();
                    RenderList(displayList, pageCount);

                }
                else
                {
                    var displayList = MasterFileList.OrderByDescending(p => p.CreatedDateTicks).Skip(MediaPanelPage * Conf.PreviewItems).Take(Conf.PreviewItems).ToList();
                    int pageCount = (MasterFileList.Count - 1) / Conf.PreviewItems + 1;
                    RenderList(displayList,pageCount);
                }
               
                
                   
            }
        }

        private void RenderList(List<FilePreview> l, int pageCount )
        {
            
            flowPreview.SuspendLayout();
            llblPage.Text = String.Format("{0} / {1}", (MediaPanelPage + 1), pageCount);

            var currentList = new List<PreviewBox>();
            
            for (int i = 0; i < flowPreview.Controls.Count; i++)
            {
                var pb = flowPreview.Controls[i] as PreviewBox;
                if (pb != null)
                {
                    if (l.Count(p => p.CreatedDateTicks == pb.CreatedDate.Ticks) == 0)
                    {
                        flowPreview.Controls.Remove(pb);
                        pb.MouseDown -= PbMouseDown;
                        pb.MouseEnter -= PbMouseEnter;
                        pb.Dispose();
                        i--;
                    }
                    else
                    {
                        currentList.Add(pb);
                    }
                }
                else
                {
                    var lb = flowPreview.Controls[i] as Label;
                    if (lb != null)
                    {
                        flowPreview.Controls.Remove(lb);
                        i--;
                    }
                }
            }

            int ci = 0;
            DateTime dtCurrent = DateTime.MinValue;
            bool first = true;
            foreach (FilePreview fp in l)
            {
                var dt = new DateTime(fp.CreatedDateTicks);
                if (first || dtCurrent.DayOfYear != dt.DayOfYear)
                {
                    first = false;
                    dtCurrent = dt;
                    var lb = new Label { Text = dtCurrent.ToShortDateString(), Margin = new Padding(3), Padding = new Padding(0), ForeColor = Color.White, BackColor = Color.Black, Width=96, Height=73, TextAlign = ContentAlignment.MiddleCenter};
                    flowPreview.Controls.Add(lb);
                    flowPreview.Controls.SetChildIndex(lb, ci);
                    ci++;
                }

                var pb = currentList.FirstOrDefault(p => p.CreatedDate.Ticks == fp.CreatedDateTicks);
                if (pb == null)
                {
                    FilePreview fp1 = fp;
                    switch (fp1.ObjectTypeId)
                    {
                        case 1:
                            var v = Microphones.SingleOrDefault(p => p.id == fp1.ObjectId);
                            if (v != null)
                            {
                                var filename = Conf.MediaDirectory + "audio\\" + v.directory + "\\" + fp.Filename;
                                pb = AddPreviewControl(Resources.audio, filename, fp.Duration, (new DateTime(fp.CreatedDateTicks)), v.name);
                            }
                            break;
                        case 2:
                            var c = Cameras.SingleOrDefault(p => p.id == fp1.ObjectId);
                            if (c != null)
                            {
                                var filename = Conf.MediaDirectory + "video\\" + c.directory + "\\" + fp.Filename;
                                var thumb = Conf.MediaDirectory + "video\\" + c.directory + "\\thumbs\\" +
                                            fp.Filename.Substring(0,
                                                                  fp.Filename.LastIndexOf(".", StringComparison.Ordinal)) +
                                            ".jpg";
                                pb = AddPreviewControl(thumb, filename, fp.Duration, (new DateTime(fp.CreatedDateTicks)),
                                                       c.name);
                            }
                            break;
                    }

                }
                if (pb != null)
                {
                    flowPreview.Controls.SetChildIndex(pb, ci);
                    ci++;
                }
            }

            flowPreview.ResumeLayout(true);
        }

        public void RemovePreviewByFileName(string fn)
        {
            lock (flowPreview.Controls)
            {
                for(int i=0;i<flowPreview.Controls.Count;i++)
                {
                    var pb = flowPreview.Controls[i] as PreviewBox;
                    if (pb!=null)
                    {
                        if (pb.FileName.EndsWith(fn))
                        {
                            UISync.Execute(() => RemovePreviewBox(pb));
                            return;
                        }
                    }
                }
            }
        }

        private void llblBack_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MediaPanelPage--;
            if (MediaPanelPage < 0)
                MediaPanelPage = 0;
            else
            {
                foreach (Control c in flowPreview.Controls)
                {
                     var pb = c as PreviewBox;
                     if (pb != null && pb.Selected)
                     {
                         pb.MouseDown -= PbMouseDown;
                         pb.MouseEnter -= PbMouseEnter;
                         pb.Dispose();
                     }
                }
                flowPreview.Controls.Clear();
                flowPreview.Refresh();
                LoadPreviews();
            }

        }

        private void llblNext_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MediaPanelPage++;
            if (MediaPanelPage * Conf.PreviewItems >= MasterFileList.Count)
                MediaPanelPage--;
            else
            {
                foreach (Control c in flowPreview.Controls)
                {
                    var pb = c as PreviewBox;
                    if (pb != null && pb.Selected)
                    {
                        pb.MouseDown -= PbMouseDown;
                        pb.MouseEnter -= PbMouseEnter;
                        pb.Dispose();
                    }
                }
                flowPreview.Controls.Clear();
                flowPreview.Refresh();
                LoadPreviews();
            }

        }

        private void llblPage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var p = new Pager();
            int i = MediaPanelPage;
            p.ShowDialog(this);
            if (i != MediaPanelPage)
            {
                foreach (Control c in flowPreview.Controls)
                {
                    var pb = c as PreviewBox;
                    if (pb != null && pb.Selected)
                    {
                        pb.MouseDown -= PbMouseDown;
                        pb.MouseEnter -= PbMouseEnter;
                        pb.Dispose();
                    }
                }
                flowPreview.Controls.Clear();
                flowPreview.Refresh();
                LoadPreviews();
            }
        }

    }
}