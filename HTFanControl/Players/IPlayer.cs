namespace HTFanControl.Players
{
    interface IPlayer
    {
        bool IsPlaying
        {
            get;
        }

        long VideoTime
        {
            get;
        }

        string FileName
        {
            get;
        }

        string FilePath
        {
            get;
        }

        string ErrorStatus
        {
            get;
        }

        int VideoTimeResolution
        {
            get;
        }

        bool Update();
    }
}