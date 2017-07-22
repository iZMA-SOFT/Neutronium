﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Neutronium.Core.Extension;
using Neutronium.Core.JavascriptFramework;
using Neutronium.Core.WebBrowserEngine.JavascriptObject;
using Neutronium.Core.WebBrowserEngine.Window;
using Neutronium.Core.Binding.Builder;
using Neutronium.Core.Binding.Listeners;

namespace Neutronium.Core.Binding.GlueObject
{
    public class JSCommand : GlueBase, IJSObservableBridge
    {
        private readonly HTMLViewContext _HTMLViewContext;
        private readonly IJavascriptToCSharpConverter _JavascriptToCSharpConverter;
        private readonly ICommand _Command;
        private IJavascriptObject _MappedJSValue;
        private int _Count = 1;

        public IJavascriptObject MappedJSValue => _MappedJSValue;
        public object CValue => _Command;
        public JsCsGlueType Type => JsCsGlueType.Command;
        private IWebView WebView => _HTMLViewContext.WebView;
        private IDispatcher UIDispatcher => _HTMLViewContext.UIDispatcher;
        private IJavascriptViewModelUpdater ViewModelUpdater => _HTMLViewContext.ViewModelUpdater;

        private uint _JsId;
        public uint JsId => _JsId;
        void IJSObservableBridge.SetJsId(uint jsId) => _JsId = jsId;

        private bool _InitialCanExecute = true;

        public JSCommand(HTMLViewContext context, IJavascriptToCSharpConverter converter, ICommand command)
        {
            _JavascriptToCSharpConverter = converter;
            _HTMLViewContext = context;
            _Command = command;

            try
            {
                _InitialCanExecute = _Command.CanExecute(null);
            }
            catch { }
        }

        public void GetBuildInstruction(IJavascriptObjectBuilder builder)
        {
            builder.RequestCommandCreation(_InitialCanExecute);
        }

        public void ListenChanges()
        {
            _Command.CanExecuteChanged += Command_CanExecuteChanged;
        }

        public void UnListenChanges()
        {
            _Command.CanExecuteChanged -= Command_CanExecuteChanged;
        }

        private void ExecuteCommand(IJavascriptObject[] e)
        {
            var parameter = _JavascriptToCSharpConverter.GetFirstArgumentOrNull(e);
            UIDispatcher.RunAsync(() => _Command.Execute(parameter));
        }

        private void Command_CanExecuteChanged(object sender, EventArgs e)
        {
            _Count = (_Count == 1) ? 2 : 1;
            WebView?.RunAsync(() =>
            {
                UpdateProperty("CanExecuteCount", (f) => f.CreateInt(_Count));
            });
        }

        private async void CanExecuteCommand(IJavascriptObject[] e)
        {
            var parameter = _JavascriptToCSharpConverter.GetFirstArgumentOrNull(e);
            var res = await UIDispatcher.EvaluateAsync(() => _Command.CanExecute(parameter));
            if (WebView == null)
                return;
            await WebView.RunAsync(() =>
            {
                UpdateProperty("CanExecuteValue", (f) => f.CreateBool(res));
            });
        }

        private void UpdateProperty(string propertyName, Func<IJavascriptObjectFactory, IJavascriptObject> factory)
        {
            var newValue = factory(WebView.Factory);
            ViewModelUpdater.UpdateProperty(_MappedJSValue, propertyName, newValue, new UpdateContext { ChildAllowWrite = false });
        }

        public void SetMappedJSValue(IJavascriptObject jsobject)
        {
            _MappedJSValue = jsobject;
            _MappedJSValue.Bind("Execute", WebView, ExecuteCommand);
            _MappedJSValue.Bind("CanExecute", WebView, CanExecuteCommand);
        }

        public override IEnumerable<IJSCSGlue> GetChildren()
        {
            return Enumerable.Empty<IJSCSGlue>();
        }

        protected override void ComputeString(DescriptionBuilder context)
        {
            context.AppendCommandDescription();
        }

        public void ApplyOnListenable(IObjectChangesListener listener)
        {
            listener.OnCommand(this);
        }
    }
}
