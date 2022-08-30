<#
.SYNOPSIS
    Sends GET request to SenderTestApp endpoint '/messages/poll/{queueName}' to poll messages directly from the queue.

.DESCRIPTION
    Use this function to poll messages from dead letter exchange.
#>

function Poll-Messages{
    param(
        # Array of users to create; user objects are obtained via New-User
		[Parameter(Mandatory = $true)]
        [string] $queue
    )
    $params = @{
        Uri         = $script:global.SenderUrl
        Method 		= 'GET'
        Endpoint    = "messages/poll/${queue}"
    }

    Send-Request $params
}