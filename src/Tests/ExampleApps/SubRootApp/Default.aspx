<%@ Page Title="Home Page" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="SubRootApp._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    Hello, I'm an appConfig value from <%= ConfigurationManager.AppSettings["inheritableValueFrom"] %>
</asp:Content>
