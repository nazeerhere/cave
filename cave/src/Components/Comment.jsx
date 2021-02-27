import React, { useEffect, useState } from "react";

export default function Comment() {
    const [userlist, setUser] = useState({
        username: "",
        firstname: "",
        lastname: ""
    })

    const dumb = ["some", "other", "stuff", "yeet"]

    useEffect(() => {
        fetch("http://localhost:8000/")
        .then(res => res.json())
        .then(res => {
            setUser(res)
            console.log(res)
        })
    }, [])
    // userlist.forEach(obj => console.log(obj))
    // console.log(person.ussername)
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