using ImageOrganizer.ViewModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using System;
using System.Collections.Generic;
using System.Text;

namespace ImageOrganizer
{
    public partial class ExplorerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? UnknownItemTemplate { get; set; }
        public DataTemplate? DriveTemplate { get; set; }
        public DataTemplate? FolderTemplate { get; set; }
        public DataTemplate? ImageFileTemplate { get; set; }
        public DataTemplate? VideoFileTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject? container)
        {
            if (item is ViewModelFolder folder)
                return folder.Path.EndsWith(":\\") ? DriveTemplate : FolderTemplate;
            else if (item is ImageFile)
                return ImageFileTemplate;
            else if (item is VideoFile)
                return VideoFileTemplate;
            return UnknownItemTemplate;
        }
    }
}