'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'
Imports System.Security
Imports System.Security.Principal
Imports System.Threading
Imports System.Web.Security
Imports System.IO

Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Services.Log.EventLog
Imports DotNetNuke.Services.Upgrade


Namespace DotNetNuke.Common

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Class	 : Global
    ''' 
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' 
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[sun1]	1/18/2004	Created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class [Global]
        Inherits System.Web.HttpApplication

#Region "Application Event Handlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Application_Start
        ''' Executes on the first web request into the portal application, 
        ''' when a new DLL is deployed, or when web.config is modified.
        ''' </summary>
        ''' <param name="Sender"></param>
        ''' <param name="E"></param>
        ''' <remarks>
        ''' - global variable initialization
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Application_Start(ByVal sender As Object, ByVal e As EventArgs)

            If Config.GetSetting("ServerName") = "" Then
                ServerName = Server.MachineName
            Else
                ServerName = Config.GetSetting("ServerName")
            End If

        End Sub

        Private Sub Global_BeginRequest(ByVal sender As Object, ByVal e As EventArgs) Handles Me.BeginRequest

            ' this BeginRequest method will be the first to one to fire, even if there are other HTTP Modules which hook the BeginRequest event
            Dim app As HttpApplication = CType(sender, HttpApplication)
            Dim Request As HttpRequest = app.Request

            ' all of the logic which was previously in Application_Start was moved to Init() in order to support IIS7 integrated pipeline mode ( which no longer provides access to HTTP context within Application_Start )
            Initialize.Init(app)

            ' run schedule if in Request mode
            Initialize.RunSchedule(Request)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Application_End
        ''' Executes when the Application times out
        ''' </summary>
        ''' <param name="Sender"></param>
        ''' <param name="E"></param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Application_End(ByVal sender As Object, ByVal e As EventArgs)

            ' stop scheduled jobs
            Initialize.StopScheduler()

            ' log APPLICATION_END event
            Initialize.LogEnd()

        End Sub

#End Region

    End Class

End Namespace
