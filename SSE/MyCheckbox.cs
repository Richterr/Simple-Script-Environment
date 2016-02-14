﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScriptCore;

namespace SSE
{
    class MyCheckBox:CheckBox
    {
        public ExecutableScript Script;
    }
    class MyMenuItem:MenuItem
    {
        public ExecutableScript Script;
        public MyMenuItem(string text, MenuItem[] items) : base(text, items)
        { }
    }
}
