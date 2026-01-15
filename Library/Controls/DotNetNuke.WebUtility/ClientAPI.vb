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

Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.HtmlControls
Imports System.Web.UI.WebControls

Imports DotNetNuke

Namespace DotNetNuke.UI.Utilities

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Class	 : ClientAPI
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' Library responsible for interacting with DNN Client API.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[Jon Henning]	8/3/2004	Created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class ClientAPI

#Region "Public Constants"

        Public Const SCRIPT_CALLBACKID As String = "__DNNCAPISCI"
        Public Const SCRIPT_CALLBACKTYPE As String = "__DNNCAPISCT"
        Public Const SCRIPT_CALLBACKPARAM As String = "__DNNCAPISCP"
        Public Const SCRIPT_CALLBACKPAGEID As String = "__DNNCAPISCPAGEID"
        Public Const SCRIPT_CALLBACKSTATUSID As String = "__DNNCAPISCSI"
        Public Const SCRIPT_CALLBACKSTATUSDESCID As String = "__DNNCAPISCSDI"

        Public Const DNNVARIABLE_CONTROLID As String = "__dnnVariable"
#End Region

#Region "Public Enums"

        Public Enum ClientFunctionality As Integer
            DHTML = CInt(2 ^ 0)
            XML = CInt(2 ^ 1)
            XSLT = CInt(2 ^ 2)
            Positioning = CInt(2 ^ 3)           'what we would call adaquate positioning support
            XMLJS = CInt(2 ^ 4)
            XMLHTTP = CInt(2 ^ 5)
            XMLHTTPJS = CInt(2 ^ 6)
            SingleCharDelimiters = CInt(2 ^ 7)
        End Enum

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Enumerates each namespace with a seperate js file
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Enum ClientNamespaceReferences As Integer
            dnn
            dnn_dom
            dnn_dom_positioning
            dnn_xml
            dnn_xmlhttp
        End Enum

#End Region

#Region "Private Shared Members"

        ''' -----------------------------------------------------------------------------
        ''' <summary>Private variable holding location of client side js files.  Shared by entire application.</summary>
        ''' -----------------------------------------------------------------------------
        Private Shared m_sScriptPath As String

        Private Shared m_ClientAPIDisabled As String = String.Empty

#End Region

#Region "Private Shared Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Finds __dnnVariable control on page, if not found it attempts to add its own.
        ''' </summary>
        ''' <param name="objPage">Current page rendering content</param>
        ''' <value></value>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared ReadOnly Property ClientVariableControl(ByVal objPage As Page) As HtmlInputHidden
            Get
                Return RegisterDNNVariableControl(objPage)
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Loop up parent controls to find form
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	2/2/2006	Commented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function FindForm(ByVal oCtl As Control) As Control
            Do While Not TypeOf oCtl Is HtmlControls.HtmlForm
                If oCtl Is Nothing OrElse TypeOf oCtl Is Page Then Return Nothing
                oCtl = oCtl.Parent
            Loop
            Return oCtl
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Returns __dnnVariable control if present
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	4/6/2005	Commented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetDNNVariableControl(ByVal objParent As Control) As HtmlInputHidden
            Return CType(DotNetNuke.UI.Utilities.Globals.FindControlRecursive(objParent.Page, DNNVARIABLE_CONTROLID), System.Web.UI.HtmlControls.HtmlInputHidden)
        End Function

#End Region

#Region "Public Shared Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>Character used for delimiting name from value</summary>
        ''' -----------------------------------------------------------------------------
        Public Shared ReadOnly Property COLUMN_DELIMITER() As String
            Get
                If BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters) Then
                    Return Chr(18)
                Else
                    Return "~|~"
                End If
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>Character used for delimiting name from value</summary>
        ''' -----------------------------------------------------------------------------
        Public Shared ReadOnly Property CUSTOM_COLUMN_DELIMITER() As String
            Get
                If BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters) Then
                    Return Chr(16)
                Else
                    Return "~.~"
                End If
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>Character used for delimiting name/value pairs</summary>
        ''' -----------------------------------------------------------------------------
        Public Shared ReadOnly Property CUSTOM_ROW_DELIMITER() As String
            Get
                If BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters) Then
                    Return Chr(15)
                Else
                    Return "~,~"
                End If
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>In order to reduce payload, substitute out " with different char, since when put in a hidden control it uses &quot;</summary>
        ''' -----------------------------------------------------------------------------
        Public Shared ReadOnly Property QUOTE_REPLACEMENT() As String
            Get
                If BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters) Then
                    Return Chr(19)
                Else
                    Return "~!~"
                End If
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>Character used for delimiting name/value pairs</summary>
        ''' -----------------------------------------------------------------------------
        Public Shared ReadOnly Property ROW_DELIMITER() As String
            Get
                If BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters) Then
                    Return Chr(17)
                Else
                    Return "~`~"
                End If
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Path where js files are placed
        ''' </summary>
        ''' <value></value>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/19/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Property ScriptPath() As String
            Get
                Dim script As String = ""
                If Len(m_sScriptPath) > 0 Then
                    script = m_sScriptPath
                ElseIf Not System.Web.HttpContext.Current Is Nothing Then
                    If System.Web.HttpContext.Current.Request.ApplicationPath.EndsWith("/") Then
                        script = System.Web.HttpContext.Current.Request.ApplicationPath & "js/"
                    Else
                        script = System.Web.HttpContext.Current.Request.ApplicationPath & "/js/"
                    End If
                End If
                Return script
            End Get
            Set(ByVal Value As String)
                m_sScriptPath = Value
            End Set
        End Property

#End Region

#Region "Private Shared Methods"

        Private Shared Sub AddAttribute(ByVal objControl As Control, ByVal strName As String, ByVal strValue As String)
            If TypeOf objControl Is HtmlControl Then
                CType(objControl, HtmlControl).Attributes.Add(strName, strValue)
            ElseIf TypeOf objControl Is WebControl Then
                CType(objControl, WebControl).Attributes.Add(strName, strValue)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Parses DNN Variable control contents and returns out the delimited name/value pair
        ''' </summary>
        ''' <param name="objPage">Current page rendering content</param>
        ''' <param name="strVar">Name to retrieve</param>
        ''' <returns>Delimited name/value pair string</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetClientVariableNameValuePair(ByVal objPage As Page, ByVal strVar As String) As String
            Dim ctlVar As HtmlInputHidden = ClientVariableControl(objPage)
            Dim strValue As String = ""
            If Not ctlVar Is Nothing Then strValue = ctlVar.Value
            If Len(strValue) = 0 Then strValue = System.Web.HttpContext.Current.Request(DNNVARIABLE_CONTROLID) 'using request object in case we are loading before controls have values set
            If Len(strValue) > 0 Then
                strValue = strValue.Replace(QUOTE_REPLACEMENT, """")
                Dim intIndex As Integer = strValue.IndexOf(ROW_DELIMITER & strVar & COLUMN_DELIMITER)
                If intIndex > -1 Then
                    intIndex += Len(COLUMN_DELIMITER)
                    Dim intEndIndex As Integer = strValue.IndexOf(ROW_DELIMITER, intIndex)
                    If intEndIndex > -1 Then
                        Return strValue.Substring(intIndex, intEndIndex - intIndex)
                    Else
                        Return strValue.Substring(intIndex)
                    End If
                End If
            End If
            Return ""
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Returns javascript to call dnncore.js key handler logic
        ''' </summary>
        ''' <param name="intKeyAscii">ASCII value to trap</param>
        ''' <param name="strJavascript">Javascript to execute</param>
        ''' <returns>Javascript to handle key press</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	2/17/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetKeyDownHandler(ByVal intKeyAscii As Integer, ByVal strJavascript As String) As String
            Return "return __dnn_KeyDown('" & intKeyAscii & "', '" & strJavascript.Replace("'", "%27") & "', event);"
        End Function

#End Region

#Region "Public Shared Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Common way to handle confirmation prompts on client
        ''' </summary>
        ''' <param name="objButton">Button to trap click event</param>
        ''' <param name="strText">Text to display in confirmation</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	2/17/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub AddButtonConfirm(ByVal objButton As WebControl, ByVal strText As String)
            objButton.Attributes.Add("onClick", "javascript:return confirm('" & GetSafeJSString(strText) & "');")
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Determines of browser currently requesting page adaquately supports passed un client-side functionality
        ''' </summary>
        ''' <param name="eFunctionality">Desired Functionality</param>
        ''' <returns>True when browser supports it</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function BrowserSupportsFunctionality(ByVal eFunctionality As ClientFunctionality) As Boolean
            If System.Web.HttpContext.Current Is Nothing Then Return True
            Dim blnSupports As Boolean = False

            If ClientAPIDisabled() = False Then
                Dim objCaps As BrowserCaps = BrowserCaps.GetBrowserCaps()
                If Not objCaps Is Nothing Then
                    Dim objRequest As HttpRequest = System.Web.HttpContext.Current.Request
                    Dim strUserAgent As String = objRequest.UserAgent
                    If Len(strUserAgent) > 0 Then
                        'First check whether we have checked this browser before
                        If objCaps.FunctionalityDictionary.ContainsKey(strUserAgent) = False Then
                            Dim strBrowser As String = objRequest.Browser.Browser
                            Dim dblVersion As Double = CDbl(objRequest.Browser.MajorVersion + objRequest.Browser.MinorVersion)
                            Dim iBitValue As Integer = 0
                            Dim objFuncInfo As FunctionalityInfo = Nothing
                            'loop through all functionalities for this UserAgent and determine the bitvalue 
                            For Each eFunc As ClientFunctionality In System.Enum.GetValues(GetType(ClientFunctionality))
                                objFuncInfo = objCaps.Functionality(eFunc)
                                If objFuncInfo.HasMatch(strUserAgent, strBrowser, dblVersion) Then
                                    iBitValue += eFunc
                                End If
                            Next
                            objCaps.FunctionalityDictionary(strUserAgent) = iBitValue
                        End If
                        blnSupports = (DirectCast(objCaps.FunctionalityDictionary(strUserAgent), Integer) And eFunctionality) <> 0
                    End If
                End If
            End If

            Return blnSupports

        End Function

        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String) As String
            Return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, Nothing, "")
        End Function

        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String, ByVal eCallbackType As ClientAPICallBackResponse.CallBackTypeCode) As String
            Return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, Nothing, Nothing, eCallbackType)
        End Function

        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String, ByVal objPostChildrenOf As Control) As String
            Return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, Nothing, objPostChildrenOf.ClientID, ClientAPICallBackResponse.CallBackTypeCode.Simple)
        End Function

        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String, ByVal strClientStatusCallBack As String) As String
            Return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, Nothing, ClientAPICallBackResponse.CallBackTypeCode.Simple)
        End Function

        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String, ByVal strClientStatusCallBack As String, ByVal eCallbackType As ClientAPICallBackResponse.CallBackTypeCode) As String
            Return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, Nothing, eCallbackType)
        End Function

        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String, ByVal strClientStatusCallBack As String, ByVal objPostChildrenOf As Control) As String
            Return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, objPostChildrenOf.ClientID, ClientAPICallBackResponse.CallBackTypeCode.Simple)
        End Function

        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String, ByVal strClientStatusCallBack As String, ByVal strPostChildrenOfId As String) As String
            Return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, strPostChildrenOfId, ClientAPICallBackResponse.CallBackTypeCode.Simple)
        End Function
        Public Shared Function GetCallbackEventReference(ByVal objControl As Control, ByVal strArgument As String, ByVal strClientCallBack As String, ByVal strContext As String, ByVal srtClientErrorCallBack As String, ByVal strClientStatusCallBack As String, ByVal strPostChildrenOfId As String, ByVal eCallbackType As ClientAPICallBackResponse.CallBackTypeCode) As String
            Dim strCallbackType As String = CInt(eCallbackType).ToString
            If strArgument Is Nothing Then strArgument = "null"
            If strContext Is Nothing Then strContext = "null"
            If srtClientErrorCallBack Is Nothing Then srtClientErrorCallBack = "null"
            If strClientStatusCallBack Is Nothing Then strClientStatusCallBack = "null"
            If Len(strPostChildrenOfId) = 0 Then
                strPostChildrenOfId = "null"
            ElseIf strPostChildrenOfId.StartsWith("'") = False Then
                strPostChildrenOfId = "'" & strPostChildrenOfId & "'"
            End If
            Dim strControlID As String = objControl.ID
            If BrowserSupportsFunctionality(ClientFunctionality.XMLHTTP) AndAlso BrowserSupportsFunctionality(ClientFunctionality.XML) Then
                DotNetNuke.UI.Utilities.ClientAPI.RegisterClientReference(objControl.Page, DotNetNuke.UI.Utilities.ClientAPI.ClientNamespaceReferences.dnn_xml)
                DotNetNuke.UI.Utilities.ClientAPI.RegisterClientReference(objControl.Page, DotNetNuke.UI.Utilities.ClientAPI.ClientNamespaceReferences.dnn_xmlhttp)

                If TypeOf (objControl) Is Page AndAlso Len(strControlID) = 0 Then                 'page doesn't usually have an ID so we need to make one up
                    strControlID = SCRIPT_CALLBACKPAGEID
                End If

                If TypeOf (objControl) Is Page = False Then
                    strControlID = strControlID & " " & objControl.ClientID                   'ID is not unique (obviously)
                End If

                Return String.Format("dnn.xmlhttp.doCallBack('{0}',{1},{2},{3},{4},{5},{6},{7},{8});", strControlID, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, "null", strPostChildrenOfId, strCallbackType)
            Else
                Return ""
            End If

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Retrieves DNN Client Variable value
        ''' </summary>
        ''' <param name="objPage">Current page rendering content</param>
        ''' <param name="strVar">Variable name to retrieve value for</param>
        ''' <returns>Value of variable</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetClientVariable(ByVal objPage As Page, ByVal strVar As String) As String
            Dim strPair As String = GetClientVariableNameValuePair(objPage, strVar)
            If strPair.IndexOf(COLUMN_DELIMITER) > -1 Then
                Return Split(strPair, COLUMN_DELIMITER)(1)
            Else
                Return ""
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Retrieves DNN Client Variable value
        ''' </summary>
        ''' <param name="objPage">Current page rendering content</param>
        ''' <param name="strVar">Variable name to retrieve value for</param>
        ''' <param name="strDefaultValue">Default value if variable not found</param>
        ''' <returns>Value of variable</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetClientVariable(ByVal objPage As Page, ByVal strVar As String, ByVal strDefaultValue As String) As String
            Dim strPair As String = GetClientVariableNameValuePair(objPage, strVar)
            If strPair.IndexOf(COLUMN_DELIMITER) > -1 Then
                Return Split(strPair, COLUMN_DELIMITER)(1)
            Else
                Return strDefaultValue
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Escapes string to be safely used in client side javascript.  
        ''' </summary>
        ''' <param name="strString">String to escape</param>
        ''' <returns>Escaped string</returns>
        ''' <remarks>
        ''' Currently this only escapes out quotes and apostrophes
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	2/17/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSafeJSString(ByVal strString As String) As String
            If Len(strString) > 0 Then
                'Return System.Text.RegularExpressions.Regex.Replace(strString, "(['""])", "\$1")
                Return System.Text.RegularExpressions.Regex.Replace(strString, "(['""\\])", "\$1")
            Else
                Return strString
            End If
        End Function

        Public Shared Function IsInCallback(ByVal objPage As Page) As Boolean
            Return Len(objPage.Request(SCRIPT_CALLBACKID)) > 0
        End Function

        Public Shared Sub HandleClientAPICallbackEvent(ByVal objPage As Page)
            Dim eType As ClientAPICallBackResponse.CallBackTypeCode = ClientAPICallBackResponse.CallBackTypeCode.Simple
            If Len(objPage.Request(SCRIPT_CALLBACKTYPE)) > 0 Then
                eType = CType(objPage.Request(SCRIPT_CALLBACKTYPE), ClientAPICallBackResponse.CallBackTypeCode)
            End If
            HandleClientAPICallbackEvent(objPage, eType)
        End Sub

        Public Shared Sub HandleClientAPICallbackEvent(ByVal objPage As Page, ByVal eType As ClientAPICallBackResponse.CallBackTypeCode)
            If IsInCallback(objPage) Then
                If eType <> ClientAPICallBackResponse.CallBackTypeCode.ProcessPage Then
                    Dim arrIDs() As String = objPage.Request(SCRIPT_CALLBACKID).Split(CChar(" "))
                    Dim strControlID As String = arrIDs(0)
                    Dim strClientID As String = ""
                    If arrIDs.Length > 1 Then
                        strClientID = arrIDs(1)
                    End If

                    Dim strParam As String = objPage.Server.UrlDecode(objPage.Request(SCRIPT_CALLBACKPARAM))
                    Dim objControl As Control
                    Dim objInterface As IClientAPICallbackEventHandler
                    Dim objResponse As ClientAPICallBackResponse = New ClientAPICallBackResponse(objPage, ClientAPICallBackResponse.CallBackTypeCode.Simple)

                    Try
                        objPage.Response.Clear()                      'clear response stream
                        If strControlID = SCRIPT_CALLBACKPAGEID Then
                            objControl = objPage
                        Else
                            objControl = Globals.FindControlRecursive(objPage, strControlID, strClientID)
                        End If
                        If Not objControl Is Nothing Then
                            If TypeOf (objControl) Is System.Web.UI.HtmlControls.HtmlForm Then                        'form doesn't implement interface, so use page instead
                                objInterface = CType(objPage, IClientAPICallbackEventHandler)
                            Else
                                objInterface = CType(objControl, IClientAPICallbackEventHandler)
                            End If

                            If Not objInterface Is Nothing Then
                                Try
                                    objResponse.Response = objInterface.RaiseClientAPICallbackEvent(strParam)
                                    objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.OK
                                Catch ex As Exception
                                    objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.GenericFailure
                                    objResponse.StatusDesc = ex.Message
                                End Try
                            Else
                                objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.InterfaceNotSupported
                                objResponse.StatusDesc = "Interface Not Supported"
                            End If
                        Else
                            objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.ControlNotFound
                            objResponse.StatusDesc = "Control Not Found"
                        End If
                    Catch ex As Exception
                        objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.GenericFailure
                        objResponse.StatusDesc = ex.Message
                    Finally
                        objResponse.Write()
                        'objPage.Response.Flush()
                        objPage.Response.End()
                    End Try

                Else
                    objPage.SetRenderMethodDelegate(AddressOf CallbackRenderMethod)
                End If
            End If
        End Sub

        Private Shared Sub CallbackRenderMethod(ByVal output As System.Web.UI.HtmlTextWriter, ByVal container As System.Web.UI.Control)
            Dim objPage As Page = CType(container, Page)
            HandleClientAPICallbackEvent(objPage, ClientAPICallBackResponse.CallBackTypeCode.Simple)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Determines if DNNVariable control is present in page's control collection
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	4/6/2005	Commented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function NeedsDNNVariable(ByVal objParent As Control) As Boolean
            Return GetDNNVariableControl(objParent) Is Nothing
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Responsible for registering client side js libraries and its dependecies.
        ''' </summary>
        ''' <param name="objPage">Current page rendering content</param>
        ''' <param name="eRef">Enumerator of library to reference</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub RegisterClientReference(ByVal objPage As Page, ByVal eRef As ClientNamespaceReferences)
            Select Case eRef
                Case ClientNamespaceReferences.dnn
                    If Not IsClientScriptBlockRegistered(objPage, "dnn.js") Then
                        RegisterClientScriptBlock(objPage, "dnn.js", "<script src=""" & ClientAPI.ScriptPath & "dnn.js""></script>")
                        If BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters) = False Then
                            RegisterClientVariable(objPage, "__scdoff", "1", True)                           'SingleCharDelimiters Off!!!
                        End If
                    End If
                Case ClientNamespaceReferences.dnn_dom
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn)
                Case ClientNamespaceReferences.dnn_dom_positioning
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn)
                    If Not IsClientScriptBlockRegistered(objPage, "dnn.positioning.js") Then
                        RegisterClientScriptBlock(objPage, "dnn.positioning.js", "<script src=""" & ClientAPI.ScriptPath & "dnn.dom.positioning.js""></script>")
                    End If
                Case ClientNamespaceReferences.dnn_xml
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn)

                    If Not IsClientScriptBlockRegistered(objPage, "dnn.xml.js") Then
                        Dim strScript As String = "<script src=""" & ClientAPI.ScriptPath & "dnn.xml.js""></script>"
                        'only register the js parser if browsers needs it
                        If BrowserSupportsFunctionality(ClientFunctionality.XMLJS) Then strScript &= "<script src=""" & ClientAPI.ScriptPath & "dnn.xml.jsparser.js""></script>" 'TODO: detect when using uplevel parser and only send this when necessary
                        RegisterClientScriptBlock(objPage, "dnn.xml.js", strScript)
                    End If
                Case ClientNamespaceReferences.dnn_xmlhttp
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn)
                    If Not IsClientScriptBlockRegistered(objPage, "dnn.xmlhttp.js") Then
                        Dim strScript As String = "<script src=""" & ClientAPI.ScriptPath & "dnn.xmlhttp.js""></script>"
                        If BrowserSupportsFunctionality(ClientFunctionality.XMLHTTPJS) Then strScript &= "<script src=""" & ClientAPI.ScriptPath & "dnn.xmlhttp.jsxmlhttprequest.js""></script>"
                        RegisterClientScriptBlock(objPage, "dnn.xmlhttp.js", strScript)
                    End If
            End Select
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Registers a client side variable (name/value) pair
        ''' </summary>
        ''' <param name="objPage">Current page rendering content</param>
        ''' <param name="strVar">Variable name</param>
        ''' <param name="strValue">Value</param>
        ''' <param name="blnOverwrite">Determins if a replace or append is applied when variable already exists</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	8/3/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub RegisterClientVariable(ByVal objPage As Page, ByVal strVar As String, ByVal strValue As String, ByVal blnOverwrite As Boolean)
            'only add once
            Dim ctlVar As HtmlInputHidden = ClientVariableControl(objPage)
            Dim strPair As String = GetClientVariableNameValuePair(objPage, strVar)
            If strPair.Length > 0 Then
                strPair = strPair.Replace("""", ClientAPI.QUOTE_REPLACEMENT)                   'since we are searching for existing string we need it in its posted format (without quotes)
                If blnOverwrite Then
                    ctlVar.Value = ctlVar.Value.Replace(ROW_DELIMITER & strPair, ROW_DELIMITER & strVar & COLUMN_DELIMITER & strValue)
                Else
                    'appending value
                    Dim strOrig As String = GetClientVariable(objPage, strVar)
                    ctlVar.Value = ctlVar.Value.Replace(ROW_DELIMITER & strPair, ROW_DELIMITER & strVar & COLUMN_DELIMITER & strOrig & strValue)
                End If
            Else
                ctlVar.Value &= ROW_DELIMITER & strVar & COLUMN_DELIMITER & strValue
            End If
            ctlVar.Value = ctlVar.Value.Replace("""", ClientAPI.QUOTE_REPLACEMENT)            'reduce payload of &quot;

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Responsible for inputting the hidden field necessary for the ClientAPI to pass variables back in forth
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	4/6/2005	Commented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function RegisterDNNVariableControl(ByVal objParent As System.Web.UI.Control) As HtmlInputHidden
            Dim ctlVar As System.Web.UI.HtmlControls.HtmlInputHidden = GetDNNVariableControl(objParent)
            If ctlVar Is Nothing Then
                Dim oForm As Control = FindForm(objParent)
                If Not oForm Is Nothing Then
                    ctlVar = New System.Web.UI.HtmlControls.HtmlInputHidden
                    ctlVar.ID = DNNVARIABLE_CONTROLID
                    'oForm.Controls.AddAt(0, ctlVar)
                    oForm.Controls.Add(ctlVar)
                End If
            End If
            Return ctlVar
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Traps client side keydown event looking for passed in key press (ASCII) and hooks it up with server side postback handler
        ''' </summary>
        ''' <param name="objControl">Control that should trap the keydown</param>
        ''' <param name="objPostbackControl">Server-side control that has its onclick event handled server-side</param>
        ''' <param name="intKeyAscii">ASCII value of key to trap</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	2/17/2005	Commented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub RegisterKeyCapture(ByVal objControl As Control, ByVal objPostbackControl As Control, ByVal intKeyAscii As Integer)
            Globals.SetAttribute(objControl, "onkeydown", GetKeyDownHandler(intKeyAscii, GetPostBackClientHyperlink(objPostbackControl, "")))
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Traps client side keydown event looking for passed in key press (ASCII) and hooks it up with client-side javascript
        ''' </summary>
        ''' <param name="objControl">Control that should trap the keydown</param>
        ''' <param name="strJavascript">Javascript to execute when event fires</param>
        ''' <param name="intKeyAscii">ASCII value of key to trap</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	2/17/2005	Commented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub RegisterKeyCapture(ByVal objControl As Control, ByVal strJavascript As String, ByVal intKeyAscii As Integer)
            Globals.SetAttribute(objControl, "onkeydown", GetKeyDownHandler(intKeyAscii, strJavascript))
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Allows a listener to be associated to a client side post back
        ''' </summary>
        ''' <param name="objParent">The current control on the page or the page itself.  Depending on where the page is in its lifecycle it may not be possible to add a control directly to the page object, therefore we will use the current control being rendered to append the postback control.</param>
        ''' <param name="strEventName">Name of the event to sync.  If a page contains more than a single client side event only the events associated with the passed in name will be raised.</param>
        ''' <param name="objDelegate">Server side AddressOf the function to handle the event</param>
        ''' <param name="blnMultipleHandlers">Boolean flag to determine if multiple event handlers can be associated to an event.</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	9/15/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub RegisterPostBackEventHandler(ByVal objParent As Control, ByVal strEventName As String, ByVal objDelegate As ClientAPIPostBackControl.PostBackEvent, ByVal blnMultipleHandlers As Boolean)
            Const CLIENTAPI_POSTBACKCTL_ID As String = "ClientAPIPostBackCtl"
            Dim objCtl As Control = Globals.FindControlRecursive(objParent.Page, CLIENTAPI_POSTBACKCTL_ID)           'DotNetNuke.Globals.FindControlRecursive(objParent, CLIENTAPI_POSTBACKCTL_ID)
            If objCtl Is Nothing Then
                objCtl = New ClientAPIPostBackControl(objParent.Page, strEventName, objDelegate)
                objCtl.ID = CLIENTAPI_POSTBACKCTL_ID
                objParent.Controls.Add(objCtl)
                ClientAPI.RegisterClientVariable(objParent.Page, "__dnn_postBack", GetPostBackClientHyperlink(objCtl, "[DATA]"), True)
            ElseIf blnMultipleHandlers Then
                CType(objCtl, ClientAPIPostBackControl).AddEventHandler(strEventName, objDelegate)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Registers a button inside a table for the ability to perform client-side reordering
        ''' </summary>
        ''' <param name="objButton">Button responsible for moving the row up or down.</param>
        ''' <param name="objPage">Page the table belongs to.  Can't just use objButton.Page because inside ItemCreated event of grid the button has no page yet.</param>
        ''' <param name="blnUp">Determines if the button is responsible for moving the row up or down</param>
        ''' <param name="strKey">Unique key for the table/grid to be used to obtain the new order on postback.  Needed when calling GetClientSideReOrder</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	3/10/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub EnableClientSideReorder(ByVal objButton As Control, ByVal objPage As Page, ByVal blnUp As Boolean, ByVal strKey As String)
            If DotNetNuke.UI.Utilities.ClientAPI.BrowserSupportsFunctionality(DotNetNuke.UI.Utilities.ClientAPI.ClientFunctionality.DHTML) Then
                DotNetNuke.UI.Utilities.ClientAPI.RegisterClientReference(objPage, DotNetNuke.UI.Utilities.ClientAPI.ClientNamespaceReferences.dnn_dom)
                If Not ClientAPI.IsClientScriptBlockRegistered(objPage, "dnn.util.tablereorder.js") Then
                    ClientAPI.RegisterClientScriptBlock(objPage, "dnn.util.tablereorder.js", "<script src=""" & ClientAPI.ScriptPath & "dnn.util.tablereorder.js""></script>")
                End If

                AddAttribute(objButton, "onclick", "if (dnn.util.tableReorderMove(this," & CInt(blnUp) & ",'" & strKey & "')) return false;")
                Dim objParent As Control = objButton.Parent
                While Not objParent Is Nothing
                    If TypeOf objParent Is TableRow Then
                        AddAttribute(objParent, "origidx", "-1")                        'mark row as one that we care about, it will be numbered correctly on client
                    End If
                    objParent = objParent.Parent
                End While
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Retrieves an array of the new order for the rows
        ''' </summary>
        ''' <param name="strKey">Unique key for the table/grid to be used to obtain the new order on postback.  Needed when calling GetClientSideReOrder</param>
        ''' <param name="objPage">Page the table belongs to.  Can't just use objButton.Page because inside ItemCreated event of grid the button has no page yet.</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	3/10/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetClientSideReorder(ByVal strKey As String, ByVal objPage As Page) As String()
            If Len(ClientAPI.GetClientVariable(objPage, strKey)) > 0 Then
                Return ClientAPI.GetClientVariable(objPage, strKey).Split(","c)
            Else
                Return New String() {}
            End If
        End Function

        Public Shared Function ClientAPIDisabled() As Boolean
            If m_ClientAPIDisabled = String.Empty Then
                If System.Configuration.ConfigurationManager.AppSettings("ClientAPI") Is Nothing Then
                    m_ClientAPIDisabled = "1"
                Else
                    m_ClientAPIDisabled = System.Configuration.ConfigurationManager.AppSettings("ClientAPI")
                End If
            End If
            Return m_ClientAPIDisabled = "0"
        End Function

        Public Shared Function GetPostBackClientEvent(ByVal objPage As Page, ByVal objControl As Control, ByVal arg As String) As String
            Return objPage.ClientScript.GetPostBackEventReference(objControl, arg)
        End Function
        Public Shared Function GetPostBackClientHyperlink(ByVal objControl As Control, ByVal strArgument As String) As String
            Return "javascript:" & GetPostBackEventReference(objControl, strArgument)
        End Function
        Public Shared Function GetPostBackEventReference(ByVal objControl As Control, ByVal strArgument As String) As String
            Return objControl.Page.ClientScript.GetPostBackEventReference(objControl, strArgument)
        End Function
        Public Shared Function IsClientScriptBlockRegistered(ByVal objPage As Page, ByVal key As String) As Boolean
            Return objPage.ClientScript.IsClientScriptBlockRegistered(objPage.GetType(), key)
        End Function
        Public Shared Sub RegisterClientScriptBlock(ByVal objPage As Page, ByVal key As String, ByVal strScript As String)
            objPage.ClientScript.RegisterClientScriptBlock(objPage.GetType(), key, strScript)
        End Sub
        Public Shared Sub RegisterStartUpScript(ByVal objPage As Page, ByVal key As String, ByVal script As String)
            objPage.ClientScript.RegisterStartupScript(objPage.GetType(), key, script)
        End Sub

#End Region

    End Class

End Namespace
