<#
.SYNOPSIS
    Sends GET request to '/messages/{id}' endpoint to retrieve message with specified identifier.

.DESCRIPTION
    After processing messages are persisted. Call this function to obtain message by ID.
#>

function Get-Message{
    param(
        # Array of users to create; user objects are obtained via New-User
		[Parameter(Mandatory = $true)]
        [string] $id
    )
    $params = @{
        Uri         = $script:global.SubscriberUrl
        Method 		= 'GET'
        Endpoint    = "messages/${id}"
    }

    Send-Request $params
}