﻿using System;
using System.Collections;
using System.Collections.Generic;
using Neutronium.Core.Binding.CollectionChanges;
using Neutronium.Core.JavascriptFramework;
using Neutronium.Core.WebBrowserEngine.JavascriptObject;
using MoreCollection.Extensions;
using Neutronium.Core.Binding.Builder;
using Neutronium.Core.Binding.Listeners;
using System.Collections.Specialized;

namespace Neutronium.Core.Binding.GlueObject
{
    internal class JSArray : GlueBase, IJSObservableBridge
    {  
        private readonly Type _IndividualType;

        public object CValue { get; }
        public List<IJSCSGlue> Items { get; }     
        public JsCsGlueType Type => JsCsGlueType.Array;
        public IJavascriptObject MappedJSValue { get; private set;  }

        private uint _JsId;
        public uint JsId => _JsId;
        void IJSObservableBridge.SetJsId(uint jsId) => _JsId = jsId;

        public JSArray(IEnumerable<IJSCSGlue> values, IEnumerable collection, Type individual)
        {
            CValue = collection;
            Items = new List<IJSCSGlue>(values);
            _IndividualType = individual; 
        }

        public void GetBuildInstruction(IJavascriptObjectBuilder builder)
        {
            builder.RequestArrayCreation(Items);
        }

        public Neutronium.Core.Binding.CollectionChanges.CollectionChanges GetChanger(JavascriptCollectionChanges changes, IJavascriptToCSharpConverter bridge)
        {
            return new CollectionChanges.CollectionChanges(bridge, changes, _IndividualType);
        }

        private void ReplayChanges(IndividualCollectionChange change, IList ilist)
        {
            switch (change.CollectionChangeType)
            {
                case CollectionChangeType.Add:
                if (change.Index == ilist.Count) 
                {
                    ilist.Add(change.Object.CValue);
                    Items.Add(change.Object);
                }
                else 
                {
                    ilist.Insert(change.Index, change.Object.CValue);
                    Items.Insert(change.Index, change.Object);
                }
                break;

                case CollectionChangeType.Remove:
                    ilist.RemoveAt(change.Index);
                    Items.RemoveAt(change.Index);
                break;
            }
        }

        public void UpdateEventArgsFromJavascript(Neutronium.Core.Binding.CollectionChanges.CollectionChanges iCollectionChanges)
        {
            var ilist = CValue as IList;
            if (ilist == null) return;

            iCollectionChanges.IndividualChanges.ForEach(c => ReplayChanges(c, ilist));
        }

        public BridgeUpdater GetAddUpdater(IJSCSGlue glue, int index)
        {
            Items.Insert(index, glue);
            return new BridgeUpdater( viewModelUpdater => Splice(viewModelUpdater, index, 0, glue));
        }

        public BridgeUpdater GetReplaceUpdater(IJSCSGlue glue, int index)
        {
            Items[index] = glue;
            return new BridgeUpdater( viewModelUpdater => Splice(viewModelUpdater, index, 1, glue));
        }

        public BridgeUpdater GetMoveUpdater(int oldIndex, int newIndex)
        {
            var item = Items[oldIndex];
            Items.RemoveAt(oldIndex);
            Items.Insert(newIndex, item);

            return new BridgeUpdater(viewModelUpdater => MoveJavascriptCollection(viewModelUpdater, item.GetJSSessionValue(), oldIndex, newIndex));
        }

        public BridgeUpdater GetRemoveUpdater(int index)
        {
            Items.RemoveAt(index);
            return new BridgeUpdater(viewModelUpdater => Splice(viewModelUpdater, index, 1));
        }

        public BridgeUpdater GetResetUpdater()
        {
            Items.Clear();
            return new BridgeUpdater(viewModelUpdater => ClearAllJavascriptCollection(viewModelUpdater));
        }

        private void Splice(IJavascriptViewModelUpdater viewModelUpdater, int index, int number, IJSCSGlue glue)
        {
            viewModelUpdater?.SpliceCollection(MappedJSValue, index, number, glue.GetJSSessionValue());
        }

        private void Splice(IJavascriptViewModelUpdater viewModelUpdater, int index, int number)
        {
            viewModelUpdater?.SpliceCollection(MappedJSValue, index, number);
        }

        private void MoveJavascriptCollection(IJavascriptViewModelUpdater viewModelUpdater, IJavascriptObject item, int oldIndex, int newIndex)
        {
            viewModelUpdater?.MoveCollectionItem(MappedJSValue, item, oldIndex, newIndex);
        }

        private void ClearAllJavascriptCollection(IJavascriptViewModelUpdater viewModelUpdater)
        {
            viewModelUpdater?.ClearAllCollection(MappedJSValue);
        }

        protected override void ComputeString(DescriptionBuilder context)
        {
            context.Append("[");
            var count = 0;
            foreach (var it in Items)
            {
                if (count!=0)
                    context.Append(",");

                using (context.PushContext(count++))
                {
                    it.BuilString(context);
                }         
            }
            context.Append("]");
        }

        public override IEnumerable<IJSCSGlue> GetChildren()
        {
            return Items;
        }

        public void SetMappedJSValue(IJavascriptObject jsobject)
        {
            MappedJSValue = jsobject;
        }

        public void ApplyOnListenable(IObjectChangesListener listener)
        {
            var notifyCollectionChanged = CValue as INotifyCollectionChanged;
            if (notifyCollectionChanged == null)
                return;

            listener.OnCollection(notifyCollectionChanged);
        }
    }
}
