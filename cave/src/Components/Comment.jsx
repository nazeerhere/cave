import React, { useEffect, useState } from "react";

export default function Comment() {
    const [userlist, setUser] = useState({
        username: "",
        firstname: "",
        lastname: ""
    })

    const dumb = ["List of task to complete for the project", "fix the input bars", "fix how to game renders to the page", "fix the login", "fix the comment url in the backend"]

    useEffect(() => {
        fetch("http://localhost:8000/")
        .then(res => res.json())
        .then(res => {
            setUser(res)
            console.log(res)
        })
    }, [])
    let arr = []

    arr.push(dumb.forEach(user => {
        let name = user.ussername
        return(name)
    }))
    console.log(arr)

    return(
        <div className="comment" >
            {dumb.map(user => {
                // let name = user.ussername
                console.log(user)
                return(
                    <div className="daBorder" >
                        <h1>{user}</h1>
                    </div>
                )
            })}
        </div>
    )
}