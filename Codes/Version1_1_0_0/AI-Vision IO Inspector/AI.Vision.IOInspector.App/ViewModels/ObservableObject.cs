using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// MVVM 바인딩을 위한 기본 알림 객체입니다.
    /// 외부 패키지 없이 WPF 기본 기능만 사용하기 위해 직접 구현합니다.
    /// </summary>
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
