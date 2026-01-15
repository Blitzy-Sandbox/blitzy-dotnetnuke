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

Imports DotNetNuke.UI.Skins

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The SkinInstaller installs Skin Components to a DotNetNuke site
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	08/22/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class SkinInstaller
        Inherits FileInstaller

#Region "Private Members"

        Private _SkinFiles As New ArrayList()
        Private _SkinName As String = Null.NullString

#End Region

#Region "Protected Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the name of the Collection Node ("skinFiles")
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/22/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides ReadOnly Property CollectionNodeName() As String
            Get
                Return "skinFiles"
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the name of the Item Node ("skinFile")
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/22/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides ReadOnly Property ItemNodeName() As String
            Get
                Return "skinFile"
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the PhysicalBasePath for the skin files
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides ReadOnly Property PhysicalBasePath() As String
            Get
                Dim _PhysicalBasePath As String = RootPath & SkinRoot & "\" & SkinName
                If Not _PhysicalBasePath.EndsWith("\") Then
                    _PhysicalBasePath += "\"
                End If
                Return _PhysicalBasePath.Replace("/", "\")
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the root folder for the Skin
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/23/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property RootPath() As String
            Get
                Dim _RootPath As String = Null.NullString
                If Package.InstallerInfo.PortalID = Null.NullInteger Then
                    _RootPath = Common.Globals.HostMapPath
                Else
                    _RootPath = PortalController.GetCurrentPortalSettings.HomeDirectoryMapPath
                End If
                Return _RootPath
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the collection of Skin Files
        ''' </summary>
        ''' <value>A List(Of InstallFile)</value>
        ''' <history>
        ''' 	[cnurse]	08/23/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property SkinFiles() As ArrayList
            Get
                Return _SkinFiles
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Name of the Skin
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/22/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property SkinName() As String
            Get
                Return _SkinName
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the name of the SkinName Node ("skinName")
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/23/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overridable ReadOnly Property SkinNameNodeName() As String
            Get
                Return "skinName"
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the RootName of the Skin
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/22/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overridable ReadOnly Property SkinRoot() As String
            Get
                Return Skins.SkinInfo.RootSkin
            End Get
        End Property

#End Region

#Region "Protected Methods"

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
        Protected Overrides Sub ProcessFile(ByVal file As InstallFile, ByVal nav As System.Xml.XPath.XPathNavigator)
            ' save the skin file for processing
            Select Case file.Extension
                Case "htm", "html", "ascx", "css"
                    If file.Path.ToLower().IndexOf(glbAboutPage.ToLower()) < 0 Then
                        SkinFiles.Add(PhysicalBasePath + file.FullName)
                    End If
            End Select

            'Call base method to set up for file processing
            MyBase.ProcessFile(file, nav)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ReadCustomManifest method reads the custom manifest items
        ''' </summary>
        ''' <param name="nav">The XPathNavigator representing the node</param>
        ''' <history>
        ''' 	[cnurse]	08/22/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub ReadCustomManifest(ByVal nav As XPathNavigator)

            'Get the Skin name
            Dim nameNav As XPathNavigator = nav.SelectSingleNode(SkinNameNodeName)
            If nameNav IsNot Nothing Then
                _SkinName = nameNav.Value
            End If

            'Call base class
            MyBase.ReadCustomManifest(nav)
        End Sub

#End Region

#Region "Public Methods"

        Public Overrides Sub Install()
            'First install (copy the files) by calling the base class
            MyBase.Install()

            ' process the list of skin files
            If SkinFiles.Count > 0 Then
                Log.StartJob(Util.SKIN_BeginProcessing)

                Dim NewSkin As New UI.Skins.SkinFileProcessor(RootPath, SkinRoot, SkinName)
                Dim strMessage As String = NewSkin.ProcessList(SkinFiles, SkinParser.Portable)
                Dim arrMessage As Array = Split(strMessage, "<br>")
                Dim strRow As String
                For Each strRow In arrMessage
                    Log.AddInfo(HtmlUtils.StripTags(strRow, True))
                Next
                Log.EndJob(Util.SKIN_EndProcessing)
            End If

        End Sub
#End Region

    End Class

End Namespace
