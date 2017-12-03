<%@ Page Title="Home Page" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="ConfigurableApp._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    Hello, I'm <%= ConfigurationManager.AppSettings["applicationName"] %>`.

    Greeting for <%= Request.Params.Get("Name") %>: <%= GreeterService.Greet(Request.Params.Get("Name")) %>
</asp:Content>
