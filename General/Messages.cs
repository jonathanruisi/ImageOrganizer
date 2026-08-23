using CommunityToolkit.Mvvm.Messaging.Messages;

using ImageOrganizer.ViewModel;

using Microsoft.UI.Xaml.Controls;

using System;
using System.Collections.Generic;
using System.Text;

namespace ImageOrganizer
{
    public class GeneralMessage { }

    public class GeneralMessage<T>
    {
        public List<T> Content { get; }

        public GeneralMessage(params T[] content)
        {
            Content = new List<T>();
            if (content != null && content.Length > 0)
                Content.AddRange(content);
        }
    }

    public class ToggleFullscreenMessage()
    {

    }

    public class SetInfoBarMessage
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public InfoBarSeverity Severity { get; set; }
        public bool IsCloseable { get; set; }
        public SetInfoBarMessage()
        {
            Title = string.Empty;
            Message = string.Empty;
            IsCloseable = true;
            Severity = InfoBarSeverity.Informational;
        }
    }

    public class RemoveImageFromCacheMessage
    {
        public string Path { get; set; }

        public RemoveImageFromCacheMessage(string path)
        {
            Path = path;
        }
    }

    public class ViewModelFolderRemovedMessage(ViewModelFolder folder)
    {
        public ViewModelFolder Folder { get; set; } = folder;
    }

    public class ImageTransformRequestMessage : RequestMessage<(double, double, double, double)>
    {

    }
}