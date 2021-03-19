import React, { useContext, useState } from "react"
import AccountInfo from "./AccountInfo"
import { UserContext } from "../../UserContext"
import Log from "./Log"

export default function Holder({ loggedIn, setLoggedIn }) {
    const status = useContext(UserContext)
    console.log(loggedIn)
    console.log(status)
    setLoggedIn(status)
    if(loggedIn) {
        return(
            <div className="AI" >
            <AccountInfo  setLoggedIn={setLoggedIn} />
        </div>
        )
    } else {
        return(
            <div className="AI" >
                <Log props={loggedIn} setLoggedIn={setLoggedIn} />
            </div>
        )
    }
}