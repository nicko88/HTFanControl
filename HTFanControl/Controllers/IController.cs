namespace HTFanControl.Controllers
{
    interface IController
    {
        string ErrorStatus
        {
            get;
        }

        bool Connect();
        bool SendCMD(string cmd);
    }
}