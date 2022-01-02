namespace HTFanControl.Controllers
{
    interface IController
    {
        string ErrorStatus
        {
            get;
        }

        bool Connect();
        void Disconnect();
        bool SendCMD(string cmd);
    }
}