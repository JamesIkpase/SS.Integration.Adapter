﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using log4net.Util;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class FixtureOverview
    {
        private IDictionary<string, PropertyChanged> _changes;
        private bool? _isStreaming;
        private bool? _isDeleted;
        private bool? _isErrored;
        private bool? _isOver;
        private string _name;
        private DateTime _timeStamp;
        private MatchStatus? _matchStatus;
        private int? _sequence;
        private int? _epoch;
        private Exception _lastError;
        private bool? _isSuspended;


        public FixtureOverview()
        {
            _changes = new Dictionary<string, PropertyChanged>();
        }

        public string Id { get; set; }

        public string Name
        {
            get { return _name; }
            set
            {
                OnChanged(_name, value);
                _name = value;
            }
        }

        public int? Epoch
        {
            get { return _epoch; }
            set
            {
                OnChanged(_epoch,value);
                _epoch = value;
            }
        }

        public DateTime TimeStamp
        {
            get { return _timeStamp; }
            private set { _timeStamp = value; }
        }

        public MatchStatus? MatchStatus
        {
            get { return _matchStatus; }
            set
            {
                OnChanged(_matchStatus, value);
                _matchStatus = value;
            }
        }

        public int? Sequence
        {
            get { return _sequence; }
            set
            {
                OnChanged(_sequence, value);
                _sequence = value;
            }
        }


        public bool? IsStreaming
        {
            get { return _isStreaming; }
            set
            {
                OnChanged(_isStreaming,value);
                _isStreaming = value;
            } 
        }

        public bool? IsDeleted
        {
            get { return _isDeleted; }
            set
            {
                OnChanged(_isDeleted, value);
                _isDeleted = value;
            }
        }

        public bool? IsErrored
        {
            get { return _isErrored; }
            set
            {
                OnChanged(_isErrored, value);
                _isErrored = value;
            }
        }

        public bool? IsSuspended
        {
            get { return _isSuspended; }
            set
            {
                OnChanged(_isSuspended, value);
                _isSuspended = value;
            }
        }

        public Exception LastError
        {
            get { return _lastError; }
            set
            {
                OnChanged(_lastError != null ? _lastError.ToString() : null,value != null ? value.ToString() : null);
                _lastError = value;
            }
        }

        public bool? IsOver
        {
            get { return _isOver; }
            set { 
                OnChanged(_isOver,value);
                _isOver = value; 
            }
        }
        
        private void OnChanged<T>(T? oldValue, T? newValue, [CallerMemberName] string callerName = null) where T:struct
        {
            var oldValueString = oldValue.HasValue ? oldValue.Value.ToString() : null;
            OnChanged(oldValueString,newValue.ToString(),callerName);
        }

        private void OnChanged(string oldValue,string newValue, [CallerMemberName] string callerName = null)
        {
            var propertyChanged = new PropertyChanged
            {
                CurrentValue = newValue,
                PreviousValue = oldValue,
                ItemName = callerName
            };

            propertyChanged.SetTimeStamp();
            TimeStamp = propertyChanged.TimeStamp;

            _changes[callerName] = propertyChanged;
        }
        
        public IEnumerable<PropertyChanged> GetChanges()
        {
            return _changes.Values;
        }

        public void Merge(FixtureOverview other)
        {
            //other._changes 

        }
    }
}