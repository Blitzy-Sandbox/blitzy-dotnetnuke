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

Imports System.Collections.Generic
Imports System.Data.Common
Imports System.IO
Imports System.Text
Imports System.Xml.XPath

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The ScriptInstaller installs Script Components to a DotNetNuke site
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	08/07/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class ScriptInstaller
        Inherits FileInstaller

#Region "Private Members"

        Private _InstallScripts As New SortedList(Of String, InstallFile)
        Private _Transaction As DbTransaction
        Private _UnInstallScripts As New SortedList(Of String, InstallFile)

#End Region

#Region "Protected Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the collection of Install Scripts
        ''' </summary>
        ''' <value>A List(Of InstallFile)</value>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property InstallScripts() As SortedList(Of String, InstallFile)
            Get
                Return _InstallScripts
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the collection of UnInstall Scripts
        ''' </summary>
        ''' <value>A List(Of InstallFile)</value>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property UnInstallScripts() As SortedList(Of String, InstallFile)
            Get
                Return _UnInstallScripts
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the name of the Collection Node ("scripts")
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides ReadOnly Property CollectionNodeName() As String
            Get
                Return "scripts"
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the name of the Item Node ("script")
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides ReadOnly Property ItemNodeName() As String
            Get
                Return "script"
            End Get
        End Property

        Protected ReadOnly Property ProviderConfiguration() As Framework.Providers.ProviderConfiguration
            Get
                Return Framework.Providers.ProviderConfiguration.GetProviderConfiguration("data")
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Database Transaction
        ''' </summary>
        ''' <value>A DbTransaction object</value>
        ''' <history>
        ''' 	[cnurse]	08/08/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property Transaction() As DbTransaction
            Get
                If _Transaction Is Nothing Then
                    _Transaction = DataProvider.Instance.GetTransaction()
                End If
                Return _Transaction
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Function ExecuteSql(ByVal scriptFile As InstallFile, ByVal useTransaction As Boolean) As Boolean

            Dim bSuccess As Boolean = True

            Log.AddInfo(String.Format(Util.SQL_BeginFile, scriptFile.Name))

            ' read script file for installation
            Dim objStreamReader As StreamReader
            objStreamReader = File.OpenText(PhysicalBasePath + scriptFile.FullName)
            Dim strScript As String = objStreamReader.ReadToEnd
            objStreamReader.Close()

            'This check needs to be included because the unicode Byte Order mark results in an extra character at the start of the file
            'The extra character - '?' - causes an error with the database.
            If strScript.StartsWith("?") Then
                strScript = strScript.Substring(1)
            End If

            Dim strSQLExceptions As String = Null.NullString

            If useTransaction Then
                strSQLExceptions = DataProvider.Instance.ExecuteScript(strScript, Transaction)
            Else
                strSQLExceptions = DataProvider.Instance.ExecuteScript(strScript)
            End If

            If strSQLExceptions <> "" Then
                Log.AddFailure(String.Format(Util.SQL_Exceptions, vbCrLf, strSQLExceptions))
                bSuccess = False
            End If

            Log.AddInfo(String.Format(Util.SQL_EndFile, scriptFile.Name))

            Return bSuccess
        End Function

#End Region

#Region "Protected Methods"

        Protected Overrides Sub DeleteFile(ByVal scriptFile As InstallFile)
            'Process the file if it is an UnInstall Script
            If UnInstallScripts.ContainsValue(scriptFile) AndAlso ProviderConfiguration.DefaultProvider.ToLower = Path.GetExtension(scriptFile.Name.ToLower).Substring(1) Then
                If scriptFile.Name.ToLower.StartsWith("uninstall.") Then
                    'Install Script
                    Log.AddInfo(Util.SQL_Executing + scriptFile.Name)
                    ExecuteSql(scriptFile, False)
                End If
            End If

            'Call base method to delete file
            MyBase.DeleteFile(scriptFile)
        End Sub

        Protected Overrides Function InstallFile(ByVal scriptFile As InstallFile) As Boolean
            'Call base method to copy file
            Dim bSuccess As Boolean = MyBase.InstallFile(scriptFile)

            'Process the file if it is an Install Script
            If InstallScripts.ContainsValue(scriptFile) AndAlso ProviderConfiguration.DefaultProvider.ToLower = Path.GetExtension(scriptFile.Name.ToLower).Substring(1) Then
                If scriptFile.Name.ToLower.StartsWith("install.") And Package.InstalledVersion = "00.00.00" Then
                    'Install Script
                    Log.AddInfo(Util.SQL_Executing + scriptFile.Name)
                    bSuccess = ExecuteSql(scriptFile, True)
                Else
                    'Upgrade Script
                    If scriptFile.Version > Package.InstalledVersion Then
                        'Script represents an Upgrade Script file for a newer version than Installed Version
                        Log.AddInfo(Util.SQL_Executing + scriptFile.Name)
                        bSuccess = ExecuteSql(scriptFile, True)
                    End If
                End If
            End If

            Return bSuccess
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a flag that determines what type of file this installer supports
        ''' </summary>
        ''' <param name="type">The type of file being processed</param>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function IsCorrectType(ByVal type As InstallFileType) As Boolean
            Return (type = InstallFileType.Script)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ProcessFile method determines what to do with parsed "file" node
        ''' </summary>
        ''' <param name="file">The file represented by the node</param>
        ''' <param name="nav">The XPathNavigator representing the node</param>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub ProcessFile(ByVal file As InstallFile, ByVal nav As XPathNavigator)
            Dim type As String = nav.GetAttribute("type", "")

            If file IsNot Nothing AndAlso IsCorrectType(file.Type) Then
                If type.ToLower = "install" Then
                    InstallScripts.Add(file.Version, file)
                Else
                    UnInstallScripts.Add(file.Version, file)
                End If
            End If

            'Call base method to set up for file processing
            MyBase.ProcessFile(file, nav)
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Commit method finalises the Install and commits any pending changes.
        ''' </summary>
        ''' <remarks>In the case of Files this is not neccessary</remarks>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub Commit()
            Try
                DataProvider.Instance.CommitTransaction(Transaction)

                If Transaction.Connection IsNot Nothing Then
                    Transaction.Connection.Close()
                End If
                Log.AddInfo(Util.SQL_Committed)
            Catch ex As Exception
                Log.AddFailure(Util.EXCEPTION + " - " + ex.Message)
            End Try

            'Call base method
            MyBase.Commit()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Install method installs the script component
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub Install()
            Log.AddInfo(Util.SQL_Begin)

            'Call the base method
            MyBase.Install()

            Log.AddInfo(Util.SQL_End)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Rollback method undoes the installation of the script component in the event 
        ''' that one of the other components fails
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub Rollback()
            Try
                DataProvider.Instance.RollbackTransaction(Transaction)

                If Transaction.Connection IsNot Nothing Then
                    Transaction.Connection.Close()
                End If
                Log.AddInfo(Util.SQL_RolledBack)
            Catch ex As Exception
                Log.AddFailure(Util.EXCEPTION + " - " + ex.Message)
            End Try

            'Call base method
            MyBase.Rollback()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The UnInstall method uninstalls the script component
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub UnInstall()
            Log.AddInfo(Util.SQL_BeginUnInstall)

            'Call the base method
            MyBase.UnInstall()

            Log.AddInfo(Util.SQL_EndUnInstall)
        End Sub

#End Region

    End Class

End Namespace
