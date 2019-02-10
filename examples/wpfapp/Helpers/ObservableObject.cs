using System.ComponentModel;

namespace wpfapp.Helpers
{
    public class ObservableObject<T> : INotifyPropertyChanged
    {
        private T _value = default(T);

        public T Value
        {
            get
            {
                return _value;
            }

            set
            {
                _value = value;
                this.NotifyPropertyChanged("Value");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableObject() { }

        public ObservableObject(T value)
        {
            this._value = value;
        }

        internal void NotifyPropertyChanged(string prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
    }

    public class ObservableNullable<T> : ObservableObject<T>
        where T : struct
    {
        private T _value = default(T);

        new public T Value
        {
            get
            {
                return _value;
            }

            set
            {
                _value = value;
                this.NotifyPropertyChanged("Value");
                this.NotifyPropertyChanged("NullableValue");
            }
        }

        public T? NullableValue
        {
            get
            {
                return _value;
            }
        }

        public ObservableNullable() { }

        public ObservableNullable(T value)
        {
            this._value = value;
        }
    }

    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
    }
}
