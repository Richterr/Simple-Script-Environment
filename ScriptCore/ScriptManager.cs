﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ScriptCore
{
    public class ExecutableScript
    {
        internal IExecutable Script;
        public string FileName;
        public string ScriptName;
        public bool Run;
    }

    public class ScriptManager
    {
        private List<ExecutableScript> _scripts;
        public List<ExecutableScript> Scripts { get { return _scripts; }}
        public string ErrorMessage;

        public ScriptManager(IDictionary<string, bool> scripts)
        {
            _scripts = new List<ExecutableScript>();
            string messages = "";
            foreach (var elem in scripts)
            {
                string temp;
                if (!Add(elem.Key, elem.Value, out temp))
                {
                    messages += temp + "\n";
                }
            }
            if (messages.Length > 0)
            {
                ErrorMessage = messages;
            }
        }

        public ScriptManager()
        {
            _scripts = new List<ExecutableScript>();
        }

        public bool Add(string file, bool run, out string message)
        {
            IExecutable compiled;
            try
            {
                compiled = (from type in Compile(file).GetTypes()
                            where type.GetInterfaces().Contains(typeof(IExecutable))
                            select (IExecutable)Activator.CreateInstance(type)).SingleOrDefault();
            }
            catch (NullReferenceException e)
            {
                compiled = null;
            }
            if (compiled != null)
            {

                if (!_scripts.Any((x => x.Script == compiled)))
                {
                    _scripts.Add(
                        new ExecutableScript
                        {
                            Script = compiled,
                            FileName = file,
                            Run = run,
                            ScriptName = compiled.Name
                        });
                    message = "";
                    return true;
                }
                else
                {

                    message = string.Format("{0} is already added", file);
                    return false;
                }
            }
            else
            {
                message = string.Format("{0} did not compile", file);
                return false;
            }
        }

        public bool Remove(string file)
        {
            var removable = _scripts.SingleOrDefault(x => x.FileName == file);
            if (removable != null)
            {
                _scripts.Remove(removable);
                return true;
            }
            return false;
        }

        public void Execute()
        {
            lock (_scripts)
            {
                Parallel.ForEach(_scripts, script =>
                {
                    if (script.Run)
                        script.Script.Execute();
                });
            }
        }

        private Assembly Compile(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            CodeDomProvider provider;
            bool fromFile = true;
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(fileName);
                if (fileInfo.Extension.ToLower(CultureInfo.InvariantCulture) == ".cs")
                    provider = CodeDomProvider.CreateProvider("CSharp");
                else if (fileInfo.Extension == "")
                {
                    provider = CodeDomProvider.CreateProvider("CSharp");
                    fromFile = false;
                }
                else
                    return null;
            }
            catch (ArgumentException e)
            {
                provider = CodeDomProvider.CreateProvider("CSharp");
                fromFile = false;
            }
            if (provider != null)
            {
                var compilerParameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false
                };
                compilerParameters.ReferencedAssemblies.Add("ScriptCore.dll");
                CompilerResults compilerResults;
                try
                {
                    if (fromFile)
                        compilerResults = provider.CompileAssemblyFromFile(
                        compilerParameters, fileName);
                    else
                        compilerResults = provider.CompileAssemblyFromSource(
                            compilerParameters, fileName);
                    if (compilerResults.Errors.Count != 0)
                    {
                        //MessageBox.Show(compilerResults.Errors[0].ToString());
                        return null;
                    }
                    return compilerResults.CompiledAssembly;
                }
                catch (Exception e)
                {
                }
            }
            return null;
        }
    }
}