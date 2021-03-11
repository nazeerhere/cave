import React, { useContext, useState } from "react"
import AccountInfo from "./AccountInfo"
import { UserContext } from "../../UserContext"
import Log from "./Log"

export default function Holder({ loggedIn }) {
    const status = useContext(UserContext)
    console.log(loggedIn)
    // const [logStat, setStat] = useState(status)
    console.log(status)
    if(loggedIn) {
        return(
            <div className="AI" >
            <AccountInfo/>
        </div>
        )
    } else {
        return(
            <div className="AI" >
                <Log props={loggedIn} />
            </div>
        )
    }
}