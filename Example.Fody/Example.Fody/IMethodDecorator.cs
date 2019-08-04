using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Example.Fody
{
    public class MethodDecorator
    {
        private static MethodDecorator _instance;
        #region Property
        public static MethodDecorator Instance
        {
            get
            {
                if (_instance == null) { _instance = new MethodDecorator(); }
                return _instance;
            }
        }

        public static Action<object, MethodBase, object[]> InitFun { get; set; }
        public static Action OnEntryFuc { get; set; }
        public static Action OnExitFuc { get; set; }
        public static Action<Exception> OnExceptionFuc { get; set; }
        #endregion
        public void Init(object instance, MethodBase method, object[] args)
        {
            InitFun?.Invoke(instance, method, args);

        }
        public void OnEntry()
        {
            OnEntryFuc?.Invoke();
        }
        public void OnExit()
        {
            OnEntryFuc?.Invoke();
        }
        public void OnException(Exception exception)
        {
            OnExceptionFuc?.Invoke(exception);
        }
        // Optional
        //public virtual void OnTaskContinuation(Task task) {}
    }
}
