using JLR.Utility.WinUI.ViewModel;

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace ImageOrganizer.ViewModel
{
    [ViewModelType("Folder")]
    public sealed partial class LogicalFolder : ViewModelNode
    {
        public LogicalFolder() : this(string.Empty) { }

        public LogicalFolder(string name)
        {
            Name = name;
        }
    }
}