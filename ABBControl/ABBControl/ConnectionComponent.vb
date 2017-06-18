Imports Grasshopper.Kernel
Imports Grasshopper.Kernel.Data
Imports Grasshopper.Kernel.Types
Imports robots = ABB.Robotics
Imports controls = ABB.Robotics.Controllers

Public Class ConnectionComponent
    Inherits GH_Component

    Public Sub New()
        MyBase.New("Open ABB Connection", "ABB Connection", "Opens the connection with the ABB robot", "CITA", "Robots")
    End Sub

    Public Overrides ReadOnly Property ComponentGuid As Guid
        Get
            Return New Guid("{86659EC8-0273-4005-AA76-179C8444AC1B}")
        End Get
    End Property

    Protected Overrides Sub RegisterInputParams(pManager As GH_InputParamManager)
        pManager.AddBooleanParameter("Open", "O", "Open connection", GH_ParamAccess.tree, False)
    End Sub

    Protected Overrides Sub RegisterOutputParams(pManager As GH_OutputParamManager)
    End Sub

    Public Overrides Sub RemovedFromDocument(document As GH_Document)
        CloseConnection()
        MyBase.RemovedFromDocument(document)
    End Sub

    Dim dt As New GH_Structure(Of GH_Boolean)

    Public Shared ConnectionOpened As Boolean = False
    Public Shared ABBController As controls.Controller = Nothing

    Protected Overrides Sub SolveInstance(DA As IGH_DataAccess)
        DA.GetDataTree(0, dt)

        Dim first As GH_Boolean = dt.FirstItem(True)
        If first Is Nothing Then Return
        Dim fval As Boolean = first.Value

        If fval And Not ConnectionOpened Then 'open connection 
            OpenConnection()
            Me.Message = "Opened"
        ElseIf fval And ConnectionOpened Then 'do nothing 
        ElseIf Not fval And ConnectionOpened Then 'close the connection 
            CloseConnection()
            Me.Message = "Closed"
        ElseIf Not fval And Not ConnectionOpened Then 'do nothing 
        End If

    End Sub

    Public Shared Function OpenConnection() As Boolean
        Dim scn As New controls.Discovery.NetworkScanner
        scn.Scan()
        Dim ctrl As controls.ControllerInfoCollection = scn.Controllers

        ABBController = ABB.Robotics.Controllers.ControllerFactory.CreateFrom(ctrl(0))
        ABBController.Logon(controls.UserInfo.DefaultUser)

        If ABBController.Connected Then
            ConnectionOpened = True
        Else
            ConnectionOpened = False
        End If

        Return ConnectionOpened
    End Function

    Public Shared Sub CloseConnection()
        If ConnectionOpened Then
            If ABBController IsNot Nothing Then
                ABBController.Logoff()
                ABBController.Dispose()
                ABBController = Nothing
            End If
        End If
    End Sub

End Class
