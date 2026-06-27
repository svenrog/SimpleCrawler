namespace Crawler.AngleSharp.Js.Abstractions;

internal interface IJsLocation
{
    string href { get; set; }
    string origin { get; set; }
    string protocol { get; set; }
    string host { get; set; }
    string hostname { get; set; }
    string port { get; set; }
    string pathname { get; set; }
    string search { get; set; }
    string hash { get; set; }
}
